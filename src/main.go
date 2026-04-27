package main

import (
	"fmt"
	"os"

	"github.com/Sorrow446/Nugs-Downloader/src/pkg/api"
	"github.com/Sorrow446/Nugs-Downloader/src/pkg/config"
	"github.com/Sorrow446/Nugs-Downloader/src/pkg/downloader"
	"github.com/Sorrow446/Nugs-Downloader/src/pkg/fsutil"
	"github.com/Sorrow446/Nugs-Downloader/src/pkg/logger"
	"github.com/Sorrow446/Nugs-Downloader/src/pkg/models"
	"github.com/Sorrow446/Nugs-Downloader/src/pkg/processor"
	"github.com/Sorrow446/Nugs-Downloader/src/pkg/server"
)

func main() {
	fmt.Println(`
	_____                ____                _           _
	|   | |_ _ ___ ___   |    \ ___ _ _ _ ___| |___ ___ _| |___ ___
	| | | | | | . |_ -|  |  |  | . | | | |   | | . | .'| . | -_|  _|
	|_|___|___|_  |___|  |____/|___|_____|_|_|_|___|__,|___|___|_|
	|___|`)

	// Parse configuration
	cfg, uiMode, port, err := config.ParseCfg()
	if err != nil {
		logger.GetLogger().WithError(err).Error("Failed to parse config/args")
		os.Exit(1)
	}

	if uiMode {
		srv := server.NewServer(cfg)
		if err := srv.Start(port); err != nil {
			logger.GetLogger().WithError(err).Error("Failed to start UI server")
			os.Exit(1)
		}
		return
	}

	// Create output directory

	err = fsutil.MakeDirs(cfg.OutPath)
	if err != nil {
		logger.GetLogger().WithError(err).Error("Failed to make output folder")
		os.Exit(1)
	}

	// Initialize API client
	apiClient := api.NewClient()

	// Authenticate if no token provided
	var token string
	if cfg.Token == "" {
		token, err = apiClient.Auth(cfg.Email, cfg.Password)
		if err != nil {
			logger.GetLogger().WithError(err).Error("Failed to authenticate")
			os.Exit(1)
		}
	} else {
		token = cfg.Token
	}

	// Get user info
	userId, err := apiClient.GetUserInfo(token)
	if err != nil {
		logger.GetLogger().WithError(err).Error("Failed to get user info")
		os.Exit(1)
	}

	// Get subscription info
	subInfo, err := apiClient.GetSubInfo(token)
	if err != nil {
		logger.GetLogger().WithError(err).Error("Failed to get subscription info")
		os.Exit(1)
	}

	// Extract legacy token
	legacyToken, uguID, err := models.ExtractLegToken(token)
	if err != nil {
		logger.GetLogger().WithError(err).Error("Failed to extract legacy token")
		os.Exit(1)
	}

	// Get plan description
	planDesc, isPromo := models.GetPlan(subInfo)
	if !subInfo.IsContentAccessible {
		planDesc = "no active subscription"
	}
	fmt.Println("Signed in successfully - " + planDesc + "\n")

	// Parse stream parameters
	streamParams := models.ParseStreamParams(userId, subInfo, isPromo)

	// Initialize downloader and processor
	downloader := downloader.NewDownloader(apiClient, cfg)
	processor := processor.NewProcessor(apiClient, downloader, cfg)

	// Process URLs
	albumTotal := len(cfg.Urls)
	for albumNum, url := range cfg.Urls {
		fmt.Printf("Item %d of %d:\n", albumNum+1, albumTotal)

		itemId, mediaType := models.CheckUrl(url)
		if itemId == "" {
			fmt.Println("Invalid URL:", url)
			continue
		}

		var itemErr error
		switch mediaType {
		case 0:
			itemErr = processor.ProcessAlbum(itemId, streamParams, nil)
		case 1, 2, 11:
			itemErr = processor.ProcessPlaylist(itemId, legacyToken, streamParams, false)
		case 3:
			itemErr = processor.ProcessCatalogPlist(itemId, legacyToken, streamParams)
		case 4, 10:
			itemErr = processor.ProcessVideo(itemId, "", streamParams, nil, false)
		case 5:
			itemErr = processor.ProcessArtist(itemId, streamParams)
		case 6, 7, 8:
			itemErr = processor.ProcessVideo(itemId, "", streamParams, nil, true)
		case 9:
			itemErr = processor.ProcessPaidLstream(itemId, uguID, streamParams)
		}

		if itemErr != nil {
			context := map[string]interface{}{
				"item_type": models.GetItemTypeName(mediaType),
				"item_id":   itemId,
				"item_num":  albumNum + 1,
				"total":     albumTotal,
				"url":       url,
			}
			logger.WrapError(itemErr, context)
			logger.GetLogger().Error("Item processing failed",
				"type", models.GetItemTypeName(mediaType),
				"id", itemId,
				"url", url)
		}
	}
}

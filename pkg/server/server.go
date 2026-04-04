package server

import (
	"encoding/json"
	"fmt"
	"io"
	"io/fs"
	"net/http"
	"os"
	"path/filepath"
	"strings"
	"sync"
	"time"

	"github.com/Sorrow446/Nugs-Downloader/pkg/api"
	"github.com/Sorrow446/Nugs-Downloader/pkg/config"
	"github.com/Sorrow446/Nugs-Downloader/pkg/downloader"
	"github.com/Sorrow446/Nugs-Downloader/pkg/logger"
	"github.com/Sorrow446/Nugs-Downloader/pkg/models"
	"github.com/Sorrow446/Nugs-Downloader/pkg/processor"
	"github.com/gin-contrib/cors"
	"github.com/gin-gonic/gin"
	"github.com/sirupsen/logrus"
)

var uiAssets fs.FS

func SetUIAssets(fs fs.FS) {
	uiAssets = fs
}

// ProgressState represents the current state of a download
type ProgressState struct {
	Status          string `json:"status"`
	CurrentTrack    int    `json:"currentTrack"`
	TotalTracks     int    `json:"totalTracks"`
	TrackPercentage int    `json:"trackPercentage"`
	DownloadedBytes int64  `json:"downloadedBytes"`
	TotalBytes      int64  `json:"totalBytes"`
}

// ProgressManager handles real-time progress updates
type ProgressManager struct {
	state      ProgressState
	stateMutex sync.RWMutex
	events     chan ProgressState
}

func NewProgressManager() *ProgressManager {
	return &ProgressManager{
		events: make(chan ProgressState, 10),
	}
}

func (m *ProgressManager) UpdateTrackProgress(downloaded, total int64) {
	m.stateMutex.Lock()
	m.state.DownloadedBytes = downloaded
	m.state.TotalBytes = total
	if total > 0 {
		m.state.TrackPercentage = int(float64(downloaded) / float64(total) * 100)
	}
	state := m.state
	m.stateMutex.Unlock()
	select {
	case m.events <- state:
	default:
	}
}

func (m *ProgressManager) UpdateOverallProgress(current, total int) {
	m.stateMutex.Lock()
	m.state.CurrentTrack = current
	m.state.TotalTracks = total
	state := m.state
	m.stateMutex.Unlock()
	select {
	case m.events <- state:
	default:
	}
}

func (m *ProgressManager) UpdateStatus(status string) {
	m.stateMutex.Lock()
	m.state.Status = status
	state := m.state
	m.stateMutex.Unlock()
	select {
	case m.events <- state:
	default:
	}
}

// LogHook captures logs for the UI
type LogHook struct {
	logs chan string
}

func (h *LogHook) Levels() []logrus.Level {
	return logrus.AllLevels
}

func (h *LogHook) Fire(entry *logrus.Entry) error {
	line, err := entry.String()
	if err != nil {
		return err
	}
	select {
	case h.logs <- line:
	default:
	}
	return nil
}

// Server handles the Web UI and API
type Server struct {
	config  *config.Config
	manager *ProgressManager
	logHook *LogHook
}

func NewServer(cfg *config.Config) *Server {
	manager := NewProgressManager()
	hook := &LogHook{logs: make(chan string, 100)}
	logger.GetLogger().AddHook(hook)

	return &Server{
		config:  cfg,
		manager: manager,
		logHook: hook,
	}
}

func (s *Server) Start(port int) error {
	gin.SetMode(gin.ReleaseMode)
	r := gin.Default()

	r.Use(cors.New(cors.Config{
		AllowAllOrigins: true,
		AllowMethods:    []string{"GET", "POST", "PUT", "PATCH", "DELETE", "OPTIONS"},
		AllowHeaders:    []string{"Origin", "Content-Type", "Accept"},
		MaxAge:          12 * time.Hour,
	}))

	api := r.Group("/api")
	{
		api.GET("/config", s.getConfig)
		api.POST("/config", s.saveConfig)
		api.POST("/download", s.startDownload)
		api.POST("/inspect", s.inspectUrls)
		api.GET("/events", s.streamEvents)
		api.GET("/library", s.getLibrary)
	}

	// Serve static files from embedded FS
	r.NoRoute(func(c *gin.Context) {
		path := c.Request.URL.Path
		if strings.HasPrefix(path, "/api") || uiAssets == nil {
			return
		}

		// Try to open the file in the embedded FS
		_, err := uiAssets.Open(strings.TrimPrefix(path, "/"))
		if err != nil {
			// Fallback to index.html for SPA routing
			data, err := fs.ReadFile(uiAssets, "index.html")
			if err != nil {
				c.String(http.StatusNotFound, "UI Not Found")
				return
			}
			c.Data(http.StatusOK, "text/html", data)
			return
		}

		http.FileServer(http.FS(uiAssets)).ServeHTTP(c.Writer, c.Request)
	})

	fmt.Printf("Starting UI server on http://localhost:%d\n", port)
	return r.Run(fmt.Sprintf(":%d", port))
}

func (s *Server) getConfig(c *gin.Context) {
	c.JSON(http.StatusOK, s.config)
}

func (s *Server) saveConfig(c *gin.Context) {
	var newCfg config.Config
	if err := c.ShouldBindJSON(&newCfg); err != nil {
		c.JSON(http.StatusBadRequest, gin.H{"error": err.Error()})
		return
	}

	// Update local config
	s.config.Email = newCfg.Email
	s.config.Password = newCfg.Password
	s.config.Token = newCfg.Token
	s.config.Format = newCfg.Format
	s.config.VideoFormat = newCfg.VideoFormat
	s.config.OutPath = newCfg.OutPath
	s.config.UseFfmpegEnvVar = newCfg.UseFfmpegEnvVar

	// Save to file
	data, err := json.MarshalIndent(s.config, "", "    ")
	if err != nil {
		c.JSON(http.StatusInternalServerError, gin.H{"error": "Failed to marshal config"})
		return
	}

	homeDir, _ := os.UserHomeDir()
	configPath := filepath.Join(homeDir, ".nugs-downloader", "config.json")
	if err := os.WriteFile(configPath, data, 0644); err != nil {
		c.JSON(http.StatusInternalServerError, gin.H{"error": "Failed to save config file"})
		return
	}

	c.JSON(http.StatusOK, s.config)
}

func (s *Server) getLibrary(c *gin.Context) {
	entries, err := os.ReadDir(s.config.OutPath)
	if err != nil {
		c.JSON(http.StatusInternalServerError, gin.H{"error": "Failed to read library: " + err.Error()})
		return
	}

	var artists []string
	for _, entry := range entries {
		if entry.IsDir() && !strings.HasPrefix(entry.Name(), ".") {
			artists = append(artists, entry.Name())
		}
	}
	c.JSON(http.StatusOK, artists)
}

type InspectionResult struct {
	Url       string           `json:"url"`
	ItemId    string           `json:"itemId"`
	MediaType int              `json:"mediaType"`
	TypeName  string           `json:"typeName"`
	Meta      *models.AlbArtResp `json:"meta"`
	Exists    bool             `json:"exists"`
}

func (s *Server) inspectUrls(c *gin.Context) {
	var body struct {
		Urls []string `json:"urls"`
	}
	if err := c.ShouldBindJSON(&body); err != nil {
		c.JSON(http.StatusBadRequest, gin.H{"error": err.Error()})
		return
	}

	apiClient := api.NewClient()
	token := s.config.Token
	if token == "" {
		var err error
		token, err = apiClient.Auth(s.config.Email, s.config.Password)
		if err != nil {
			c.JSON(http.StatusUnauthorized, gin.H{"error": "Auth failed: " + err.Error()})
			return
		}
	}

	results := []InspectionResult{}
	for _, url := range body.Urls {
		itemId, mediaType := models.CheckUrl(url)
		if itemId == "" {
			continue
		}

		res := InspectionResult{
			Url:       url,
			ItemId:    itemId,
			MediaType: mediaType,
			TypeName:  models.GetItemTypeName(mediaType),
		}

		// Basic check for existing folder
		// This is a bit simplified, but gives the UI something to show
		switch mediaType {
		case 0, 4, 10, 6, 7, 8, 9: // Album, Video, Livestream
			meta, err := apiClient.GetAlbumMeta(itemId)
			if err == nil {
				res.Meta = meta.Response
				artistFolder := downloader.Sanitise(meta.Response.ArtistName)
				containerFolder := downloader.Sanitise(strings.TrimRight(meta.Response.ContainerInfo, " "))
				path := filepath.Join(s.config.OutPath, artistFolder, containerFolder)
				if _, err := os.Stat(path); err == nil {
					res.Exists = true
				}
			}
		}

		results = append(results, res)
	}

	c.JSON(http.StatusOK, results)
}

func (s *Server) startDownload(c *gin.Context) {
	var body struct {
		Urls []string `json:"urls"`
	}
	if err := c.ShouldBindJSON(&body); err != nil {
		c.JSON(http.StatusBadRequest, gin.H{"error": err.Error()})
		return
	}

	go s.runDownload(body.Urls)

	c.JSON(http.StatusAccepted, gin.H{"status": "Download started"})
}

func (s *Server) runDownload(urls []string) {
	cfg := s.config
	cfg.Urls = urls

	// Re-initialize API client with current config
	apiClient := api.NewClient()
	
	// Authenticate
	var token string
	var err error
	if cfg.Token == "" {
		token, err = apiClient.Auth(cfg.Email, cfg.Password)
		if err != nil {
			s.manager.UpdateStatus(fmt.Sprintf("Auth failed: %v", err))
			return
		}
	} else {
		token = cfg.Token
	}

	userId, err := apiClient.GetUserInfo(token)
	if err != nil {
		s.manager.UpdateStatus(fmt.Sprintf("User info failed: %v", err))
		return
	}

	subInfo, err := apiClient.GetSubInfo(token)
	if err != nil {
		s.manager.UpdateStatus(fmt.Sprintf("Sub info failed: %v", err))
		return
	}

	legacyToken, uguID, err := models.ExtractLegToken(token)
	if err != nil {
		s.manager.UpdateStatus(fmt.Sprintf("Token extract failed: %v", err))
		return
	}

	planDesc, isPromo := models.GetPlan(subInfo)
	if !subInfo.IsContentAccessible {
		planDesc = "no active subscription"
	}
	s.manager.UpdateStatus("Signed in: " + planDesc)

	streamParams := models.ParseStreamParams(userId, subInfo, isPromo)
	
	dl := downloader.NewDownloaderWithReporter(apiClient, cfg, s.manager)
	proc := processor.NewProcessorWithReporter(apiClient, dl, cfg, s.manager)

	albumTotal := len(cfg.Urls)
	for albumNum, url := range cfg.Urls {
		s.manager.UpdateStatus(fmt.Sprintf("Processing item %d of %d", albumNum+1, albumTotal))
		
		itemId, mediaType := models.CheckUrl(url)
		if itemId == "" {
			continue
		}

		var itemErr error
		switch mediaType {
		case 0:
			itemErr = proc.ProcessAlbum(itemId, streamParams, nil)
		case 1, 2, 11:
			itemErr = proc.ProcessPlaylist(itemId, legacyToken, streamParams, false)
		case 3:
			itemErr = proc.ProcessCatalogPlist(itemId, legacyToken, streamParams)
		case 4, 10:
			itemErr = proc.ProcessVideo(itemId, "", streamParams, nil, false)
		case 5:
			itemErr = proc.ProcessArtist(itemId, streamParams)
		case 6, 7, 8:
			itemErr = proc.ProcessVideo(itemId, "", streamParams, nil, true)
		case 9:
			itemErr = proc.ProcessPaidLstream(itemId, uguID, streamParams)
		}

		if itemErr != nil {
			logger.GetLogger().Errorf("Item failed: %v", itemErr)
		}
	}
	s.manager.UpdateStatus("Done")
}

func (s *Server) streamEvents(c *gin.Context) {
	c.Header("Content-Type", "text/event-stream")
	c.Header("Cache-Control", "no-cache")
	c.Header("Connection", "keep-alive")
	c.Header("Transfer-Encoding", "chunked")

	c.Stream(func(w io.Writer) bool {
		select {
		case state := <-s.manager.events:
			data, _ := json.Marshal(state)
			c.SSEvent("progress", string(data))
			return true
		case log := <-s.logHook.logs:
			c.SSEvent("log", log)
			return true
		case <-c.Request.Context().Done():
			return false
		case <-time.After(15 * time.Second): // Keep-alive
			c.SSEvent("ping", "pong")
			return true
		}
	})
}

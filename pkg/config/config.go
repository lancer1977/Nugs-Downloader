package config

import (
	"encoding/json"
	"fmt"
	"os"
	"path/filepath"
	"strings"

	"github.com/alexflint/go-arg"
	"github.com/Sorrow446/Nugs-Downloader/pkg/fsutil"
	"github.com/Sorrow446/Nugs-Downloader/pkg/logger"
)

const (
	// Quality format ranges
	MinAudioFormat = 1
	MaxAudioFormat = 5
	MinVideoFormat = 1
	MaxVideoFormat = 5
)

var (
	// Resolution mappings for video formats
	resolveRes = map[int]string{
		1: "480",
		2: "720",
		3: "1080",
		4: "1440",
		5: "2160",
	}
)

// Config represents the application configuration
type Config struct {
	Email           string `json:"email"`
	Password        string `json:"password"`
	Token           string `json:"token"`
	Format          int    `json:"format"`
	VideoFormat     int    `json:"videoFormat"`
	OutPath         string `json:"outPath"`
	WantRes         string
	FfmpegNameStr   string
	Urls            []string
	ForceVideo      bool
	SkipVideos      bool
	SkipChapters    bool
	UseFfmpegEnvVar bool `json:"useFfmpegEnvVar"`
}

// Args represents command line arguments
type Args struct {
	Urls         []string `arg:"positional" help:"URLs to process"`
	Format       *int     `arg:"-f,--format" help:"Audio format (1-5)"`
	VideoFormat  *int     `arg:"-v,--video-format" help:"Video format (1-5)"`
	OutPath      string   `arg:"-o,--output" help:"Output directory"`
	ForceVideo   bool     `arg:"--force-video" help:"Force video download"`
	SkipVideos   bool     `arg:"--skip-videos" help:"Skip video downloads"`
	SkipChapters bool     `arg:"--skip-chapters" help:"Skip chapter metadata"`
	UI           bool     `arg:"--ui" help:"Start Web UI"`
	Port         int      `arg:"--port" help:"Web UI port" default:"8080"`
}

// ParseCfg returns the configuration and a boolean indicating if UI mode is requested
func ParseCfg() (*Config, bool, int, error) {
	cfg, err := readConfig()
	if err != nil {
		return nil, false, 0, fmt.Errorf("failed to read config: %w", err)
	}

	args := parseArgs()
	if args.Format != nil {
		cfg.Format = *args.Format
	}
	if args.VideoFormat != nil {
		cfg.VideoFormat = *args.VideoFormat
	}

	// Validate format ranges
	if !(cfg.Format >= MinAudioFormat && cfg.Format <= MaxAudioFormat) {
		return nil, false, 0, fmt.Errorf("track format must be between %d and %d", MinAudioFormat, MaxAudioFormat)
	}
	if !(cfg.VideoFormat >= MinVideoFormat && cfg.VideoFormat <= MaxVideoFormat) {
		return nil, false, 0, fmt.Errorf("video format must be between %d and %d", MinVideoFormat, MaxVideoFormat)
	}

	// Set resolution and output path
	cfg.WantRes = resolveRes[cfg.VideoFormat]
	if args.OutPath != "" {
		cfg.OutPath = args.OutPath
	}
	if cfg.OutPath == "" {
		cfg.OutPath = "Nugs downloads"
	}

	// Clean token
	if cfg.Token != "" {
		cfg.Token = strings.TrimPrefix(cfg.Token, "Bearer ")
	}

	// Default to "ffmpeg" in PATH.
	cfg.FfmpegNameStr = "ffmpeg"

	// If UseFfmpegEnvVar is specifically set to false, look for a local binary.
	if !cfg.UseFfmpegEnvVar {
		// Check if local ./ffmpeg exists
		localFfmpeg := "./ffmpeg"
		if _, err := os.Stat(localFfmpeg); err == nil {
			cfg.FfmpegNameStr = localFfmpeg
		} else {
			// Try executable directory
			if exePath, errExe := os.Executable(); errExe == nil {
				exeDirFfmpeg := filepath.Join(filepath.Dir(exePath), "ffmpeg")
				if _, err := os.Stat(exeDirFfmpeg); err == nil {
					cfg.FfmpegNameStr = exeDirFfmpeg
				}
			}
		}
	}

	// Process URLs
	cfg.Urls, err = processUrls(args.Urls)
	if err != nil {
		logger.GetLogger().WithError(err).Error("Failed to process URLs")
		return nil, false, 0, err
	}

	// Set flags
	cfg.ForceVideo = args.ForceVideo
	cfg.SkipVideos = args.SkipVideos
	cfg.SkipChapters = args.SkipChapters

	return cfg, args.UI, args.Port, nil
}

// readConfig reads configuration from config.json
func readConfig() (*Config, error) {
	configFilename := "config.json"
	var data []byte
	var err error

	// 1. Try ~/.nugs-downloader/config.json
	homeDir, errHome := os.UserHomeDir()
	if errHome == nil {
		homeConfigPath := filepath.Join(homeDir, ".nugs-downloader", configFilename)
		data, err = os.ReadFile(homeConfigPath)
		if err == nil {
			goto unmarshal
		}
	}

	// 2. Try current working directory
	data, err = os.ReadFile(configFilename)
	if err == nil {
		goto unmarshal
	}

	// 3. Try directory of executable
	if exePath, errExe := os.Executable(); errExe == nil {
		configPath := filepath.Join(filepath.Dir(exePath), configFilename)
		data, err = os.ReadFile(configPath)
		if err == nil {
			goto unmarshal
		}
	}

	// If not found anywhere, create a default one in ~/.nugs-downloader
	if err != nil && os.IsNotExist(err) && errHome == nil {
		homeConfigDir := filepath.Join(homeDir, ".nugs-downloader")
		err = os.MkdirAll(homeConfigDir, 0755)
		if err != nil {
			return nil, fmt.Errorf("failed to create config directory: %w", err)
		}

		defaultCfg := Config{
			Format:          4,
			VideoFormat:     5,
			OutPath:         "Nugs downloads",
			UseFfmpegEnvVar: true,
		}

		data, err = json.MarshalIndent(defaultCfg, "", "    ")
		if err != nil {
			return nil, fmt.Errorf("failed to marshal default config: %w", err)
		}

		homeConfigPath := filepath.Join(homeConfigDir, configFilename)
		err = os.WriteFile(homeConfigPath, data, 0644)
		if err != nil {
			return nil, fmt.Errorf("failed to write default config: %w", err)
		}

		// Create README.txt with explanations
		readmeContent := `Nugs Downloader Configuration

Format (Audio):
1 = 16-bit / 44.1 kHz ALAC
2 = 16-bit / 44.1 kHz FLAC
3 = 24-bit / 48 kHz MQA
4 = 360 Reality Audio / best available
5 = 150 Kbps AAC

VideoFormat:
1 = 480p
2 = 720p
3 = 1080p
4 = 1440p
5 = 4K / best available

useFfmpegEnvVar:
true  = use ffmpeg from system PATH (default)
false = use ffmpeg from binary/script directory

outPath:
Directory where downloads will be saved.
`
		readmePath := filepath.Join(homeConfigDir, "README.txt")
		_ = os.WriteFile(readmePath, []byte(readmeContent), 0644)

		// data is already populated, continue to unmarshal to return the struct
		goto unmarshal
	}

	if err != nil {
		return nil, fmt.Errorf("could not find or read %s: %w", configFilename, err)
	}

unmarshal:
	var cfg Config
	err = json.Unmarshal(data, &cfg)
	if err != nil {
		return nil, err
	}

	return &cfg, nil
}

// parseArgs parses command line arguments
func parseArgs() *Args {
	var args Args
	arg.MustParse(&args)
	return &args
}

// processUrls processes URL arguments, handling text files
func processUrls(urls []string) ([]string, error) {
	var processed []string

	for _, url := range urls {
		if strings.HasSuffix(url, ".txt") {
			lines, err := fsutil.ReadTxtFile(url)
			if err != nil {
				return nil, err
			}
			for _, line := range lines {
				if !contains(processed, line) {
					processed = append(processed, strings.TrimSuffix(line, "/"))
				}
			}
		} else {
			if !contains(processed, url) {
				processed = append(processed, strings.TrimSuffix(url, "/"))
			}
		}
	}

	return processed, nil
}

// contains checks if a slice contains a string
func contains(slice []string, item string) bool {
	for _, s := range slice {
		if strings.EqualFold(s, item) {
			return true
		}
	}
	return false
}

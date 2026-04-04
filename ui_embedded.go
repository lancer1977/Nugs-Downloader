package main

import (
	"embed"
	"io/fs"
	"github.com/Sorrow446/Nugs-Downloader/pkg/server"
)

//go:embed ui/dist
var uiAssets embed.FS

func init() {
	subFS, _ := fs.Sub(uiAssets, "ui/dist")
	server.SetUIAssets(subFS)
}

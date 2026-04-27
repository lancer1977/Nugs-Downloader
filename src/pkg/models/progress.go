package models

// ProgressReporter defines the interface for reporting download progress
type ProgressReporter interface {
	UpdateTrackProgress(downloaded, total int64)
	UpdateOverallProgress(current, total int)
	UpdateStatus(status string)
}

// NoopProgressReporter is a progress reporter that does nothing
type NoopProgressReporter struct{}

func (n *NoopProgressReporter) UpdateTrackProgress(downloaded, total int64) {}
func (n *NoopProgressReporter) UpdateOverallProgress(current, total int)    {}
func (n *NoopProgressReporter) UpdateStatus(status string)                 {}

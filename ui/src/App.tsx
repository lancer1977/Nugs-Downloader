import React, { useState, useEffect, useRef } from 'react';
import { Settings, Download, Activity, Terminal } from 'lucide-react';
import './App.css';

interface Config {
  email: string;
  password: string;
  token: string;
  format: number;
  videoFormat: number;
  outPath: string;
  useFfmpegEnvVar: boolean;
}

interface ProgressState {
  status: string;
  currentTrack: number;
  totalTracks: number;
  trackPercentage: number;
  downloadedBytes: number;
  totalBytes: number;
}

interface InspectionResult {
  url: string;
  itemId: string;
  mediaType: number;
  typeName: string;
  meta: any;
  exists: boolean;
}

function App() {
  const [config, setConfig] = useState<Config | null>(null);
  const [urls, setUrls] = useState('');
  const [inspectedItems, setInspectedItems] = useState<InspectionResult[]>([]);
  const [library, setLibrary] = useState<string[]>([]);
  const [progress, setProgress] = useState<ProgressState | null>(null);
  const [logs, setLogs] = useState<string[]>([]);
  const [isDownloading, setIsDownloading] = useState(false);
  const [isInspecting, setIsInspecting] = useState(false);
  const logEndRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    fetchConfig();
    fetchLibrary();
    const eventSource = new EventSource('http://localhost:8080/api/events');
    
    eventSource.addEventListener('progress', (e) => {
      const data = JSON.parse(e.data);
      setProgress(data);
      if (data.status === 'Done') {
        setIsDownloading(false);
        fetchLibrary();
      }
    });

    eventSource.addEventListener('log', (e) => {
      setLogs(prev => [...prev.slice(-100), e.data]);
    });

    return () => eventSource.close();
  }, []);

  useEffect(() => {
    logEndRef.current?.scrollIntoView({ behavior: 'smooth' });
  }, [logs]);

  const fetchConfig = async () => {
    try {
      const res = await fetch('http://localhost:8080/api/config');
      const data = await res.json();
      setConfig(data);
    } catch (err) {
      console.error('Failed to fetch config', err);
    }
  };

  const fetchLibrary = async () => {
    try {
      const res = await fetch('http://localhost:8080/api/library');
      const data = await res.json();
      setLibrary(data);
    } catch (err) {
      console.error('Failed to fetch library', err);
    }
  };

  const handleSaveConfig = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!config) return;
    try {
      await fetch('http://localhost:8080/api/config', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(config),
      });
      alert('Config saved!');
      fetchLibrary();
    } catch (err) {
      alert('Failed to save config');
    }
  };

  const handleInspect = async () => {
    if (!urls.trim()) return;
    setIsInspecting(true);
    try {
      const res = await fetch('http://localhost:8080/api/inspect', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ urls: urls.split('\n').filter(u => u.trim()) }),
      });
      const data = await res.json();
      setInspectedItems(data);
    } catch (err) {
      alert('Failed to inspect URLs');
    } finally {
      setIsInspecting(false);
    }
  };

  const handleStartDownload = async (selectedUrls?: string[]) => {
    const targetUrls = selectedUrls || urls.split('\n').filter(u => u.trim());
    if (targetUrls.length === 0) return;
    setIsDownloading(true);
    setLogs([]);
    try {
      await fetch('http://localhost:8080/api/download', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ urls: targetUrls }),
      });
    } catch (err) {
      alert('Failed to start download');
      setIsDownloading(false);
    }
  };

  const formatBytes = (bytes: number) => {
    if (bytes === 0) return '0 B';
    const k = 1024;
    const sizes = ['B', 'KB', 'MB', 'GB', 'TB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + sizes[i];
  };

  return (
    <div className="app">
      <header className="header">
        <h1>Nugs Downloader</h1>
        <div className="status-badge status-active">
          {isDownloading ? 'Downloading...' : 'Idle'}
        </div>
      </header>

      <div className="sidebar">
        <div className="card" style={{ marginBottom: '1rem' }}>
          <h2><Settings size={20} /> Settings</h2>
          {config && (
            <form onSubmit={handleSaveConfig}>
              <div className="form-group">
                <label>Email</label>
                <input 
                  type="email" 
                  value={config.email} 
                  onChange={e => setConfig({...config, email: e.target.value})}
                />
              </div>
              <div className="form-group">
                <label>Password</label>
                <input 
                  type="password" 
                  value={config.password} 
                  onChange={e => setConfig({...config, password: e.target.value})}
                />
              </div>
              <div className="form-group">
                <label>Audio Format</label>
                <select 
                  value={config.format} 
                  onChange={e => setConfig({...config, format: parseInt(e.target.value)})}
                >
                  <option value={1}>ALAC 16-bit</option>
                  <option value={2}>FLAC 16-bit</option>
                  <option value={3}>MQA 24-bit</option>
                  <option value={4}>360 Reality Audio</option>
                  <option value={5}>AAC 150kbps</option>
                </select>
              </div>
              <div className="form-group">
                <label>Output Path</label>
                <input 
                  type="text" 
                  value={config.outPath} 
                  onChange={e => setConfig({...config, outPath: e.target.value})}
                />
              </div>
              <button type="submit">Save Settings</button>
            </form>
          )}
        </div>

        <div className="card">
          <h2>Library</h2>
          <div className="library-list">
            {library.map(artist => (
              <div key={artist} className="library-item">{artist}</div>
            ))}
            {library.length === 0 && <div className="text-muted">Empty</div>}
          </div>
        </div>
      </div>

      <div className="main-content">
        <div className="card" style={{ marginBottom: '2rem' }}>
          <h2><Download size={20} /> Download</h2>
          <div className="form-group">
            <label>URLs (One per line)</label>
            <textarea 
              className="url-input"
              value={urls}
              onChange={e => setUrls(e.target.value)}
              placeholder="https://play.nugs.net/release/..."
            />
          </div>
          <div className="button-group">
            <button 
              onClick={handleInspect} 
              disabled={isDownloading || isInspecting || !urls.trim()}
              className="secondary"
            >
              {isInspecting ? 'Inspecting...' : 'Inspect URLs'}
            </button>
            <button 
              onClick={() => handleStartDownload()} 
              disabled={isDownloading || !urls.trim()}
            >
              Download All
            </button>
          </div>
        </div>

        {inspectedItems.length > 0 && (
          <div className="card" style={{ marginBottom: '2rem' }}>
            <h2>Inspection Results</h2>
            <div className="inspection-list">
              {inspectedItems.map((item, i) => (
                <div key={i} className={`inspection-item ${item.exists ? 'exists' : ''}`}>
                  <div className="inspection-info">
                    <strong>{item.meta ? `${item.meta.artistName} - ${item.meta.containerInfo}` : item.url}</strong>
                    <span className="type-badge">{item.typeName}</span>
                  </div>
                  <div className="inspection-actions">
                    {item.exists ? (
                      <span className="exists-badge">Already Downloaded</span>
                    ) : (
                      <button 
                        onClick={() => handleStartDownload([item.url])}
                        disabled={isDownloading}
                        className="small"
                      >
                        Download This
                      </button>
                    )}
                  </div>
                </div>
              ))}
            </div>
            <button onClick={() => setInspectedItems([])} className="text-button">Clear Results</button>
          </div>
        )}

        {progress && (
          <div className="card" style={{ marginBottom: '2rem' }}>
            <h2><Activity size={20} /> Progress</h2>
            <div className="progress-item">
              <div className="progress-info">
                <span>{progress.status}</span>
                <span>{progress.currentTrack} / {progress.totalTracks} Tracks</span>
              </div>
              <div className="progress-info">
                <span>{formatBytes(progress.downloadedBytes)} / {formatBytes(progress.totalBytes)}</span>
                <span>{progress.trackPercentage}%</span>
              </div>
              <div className="progress-bar-bg">
                <div 
                  className="progress-bar-fill" 
                  style={{ width: `${progress.trackPercentage}%` }}
                />
              </div>
            </div>
          </div>
        )}

        <div className="card">
          <h2><Terminal size={20} /> Logs</h2>
          <div className="log-viewer">
            {logs.map((log, i) => (
              <div key={i}>{log}</div>
            ))}
            <div ref={logEndRef} />
          </div>
        </div>
      </div>
    </div>
  );
}

export default App;

import { useEffect, useRef, useState } from 'react';
import {
  Activity,
  ArrowDownToLine,
  ArrowRight,
  BatteryCharging,
  Check,
  ChevronDown,
  CircleGauge,
  Clock3,
  Cpu,
  Download,
  ExternalLink,
  FileBox,
  Github,
  HardDrive,
  MemoryStick,
  Menu,
  Monitor,
  MousePointer2,
  Network,
  ShieldCheck,
  Thermometer,
  X,
  Zap,
} from 'lucide-react';

const REPO_URL = 'https://github.com/imariman/win-meter';
const DOWNLOAD_URL = `${REPO_URL}/releases/latest/download/WinMeter.exe`;
const RELEASES_URL = `${REPO_URL}/releases/latest`;

const metrics = [
  {
    id: 'cpu', label: 'CPU', icon: Cpu, color: '#60a5fa', value: '34%', unit: '4.2 GHz',
    title: 'Every core. Every demanding process.',
    description: 'Follow total processor load, clock speed and per-core activity. See which core is busiest and which processes are asking the most from your CPU.',
    details: [['Logical processors', '16'], ['Busiest core', 'Core 6 · 81%'], ['Top process', 'chrome · 12.4%']],
    points: '0,82 24,75 48,79 72,61 96,66 120,48 144,56 168,42 192,50 216,31 240,38 264,28 288,35 312,18 336,24 360,12 384,20 408,10',
  },
  {
    id: 'ram', label: 'RAM', icon: MemoryStick, color: '#4ade80', value: '81%', unit: '25.8 / 31.9 GB',
    title: 'Memory pressure, made visible.',
    description: 'See used, available and committed memory at a glance. A focused process list helps you find memory-heavy apps without opening Task Manager.',
    details: [['Available', '6.1 GB'], ['Committed', '29.4 / 38.0 GB'], ['Top process', 'Code · 2.8 GB']],
    points: '0,72 24,68 48,70 72,62 96,64 120,58 144,61 168,53 192,47 216,50 240,39 264,44 288,33 312,38 336,27 360,30 384,23 408,18',
  },
  {
    id: 'gpu', label: 'GPU', icon: Monitor, color: '#f472b6', value: '21%', unit: '3.8 / 12 GB',
    title: 'Graphics load, adapter by adapter.',
    description: 'Monitor GPU engine usage alongside dedicated and shared memory. Multi-GPU systems get a clear, separate row for every adapter.',
    details: [['Adapter', 'NVIDIA GeForce RTX 4070'], ['Dedicated memory', '3.8 / 12 GB'], ['Shared memory', '1.2 / 16 GB']],
    points: '0,86 24,82 48,84 72,74 96,80 120,68 144,73 168,61 192,70 216,57 240,63 264,48 288,56 312,45 336,54 360,38 384,46 408,32',
  },
  {
    id: 'disk', label: 'SSD', icon: HardDrive, color: '#2dd4bf', value: '62%', unit: '584 GB free',
    title: 'Storage space and throughput together.',
    description: 'Keep system-drive usage on the taskbar, then open the detail view for read/write activity, free capacity and every other fixed drive.',
    details: [['System volume', 'C: · 1.5 TB'], ['Read', '24.8 MB/s'], ['Write', '8.2 MB/s']],
    points: '0,74 24,70 48,66 72,68 96,59 120,63 144,53 168,58 192,46 216,52 240,41 264,47 288,35 312,43 336,31 360,38 384,28 408,24',
  },
  {
    id: 'network', label: 'NET', icon: Network, color: '#a78bfa', value: '3.4M', unit: '↓ 3.4 MB/s · ↑ 512 KB/s',
    title: 'Your connection, in real time.',
    description: 'Follow download and upload rates on the adapter carrying your default route. Wireless connections include Wi-Fi signal strength.',
    details: [['Adapter', 'Wi-Fi'], ['Signal', '82%'], ['Upload', '512 KB/s']],
    points: '0,88 24,86 48,82 72,85 96,64 120,76 144,49 168,67 192,35 216,60 240,28 264,52 288,39 312,58 336,25 360,42 384,18 408,32',
  },
  {
    id: 'battery', label: 'BAT', icon: BatteryCharging, color: '#facc15', value: '76%', unit: 'Plugged in',
    title: 'Battery state without the guesswork.',
    description: 'See charge, power state and remaining time on laptops. Thoughtful thresholds flag low battery before it becomes an interruption.',
    details: [['Status', 'Plugged in, charging'], ['Time remaining', '2 hr 18 min'], ['Warning level', 'Below 30%']],
    points: '0,52 24,50 48,48 72,46 96,43 120,41 144,39 168,37 192,34 216,32 240,30 264,28 288,26 312,24 336,22 360,20 384,18 408,16',
  },
  {
    id: 'temperature', label: 'TMP', icon: Thermometer, color: '#fb923c', value: '54°', unit: 'ACPI thermal zone',
    title: 'The temperatures Windows can report.',
    description: 'Surface the hottest reported ACPI thermal zone and NVIDIA GPU temperature when available. Unsupported sensors stay hidden — never invented.',
    details: [['Hottest zone', '54°C'], ['Sensors available', '1'], ['High threshold', '75°C']],
    points: '0,67 24,64 48,65 72,58 96,60 120,52 144,55 168,49 192,51 216,43 240,46 264,40 288,43 312,36 336,39 360,34 384,36 408,31',
  },
];

const faqs = [
  ['Does WinMeter need to be installed?', 'No. Download the self-contained WinMeter.exe and run it. The .NET runtime is bundled, so there is no installer and no separate .NET download.'],
  ['Why does Windows SmartScreen show a warning?', 'The executable is not code-signed yet. On first launch, choose “More info”, then “Run anyway”. Always download WinMeter from this site or the official GitHub releases page.'],
  ['Does WinMeter collect or send my data?', 'No. WinMeter makes no network calls, has no telemetry and keeps every reading on your machine. It runs as a normal user without administrator rights.'],
  ['Which Windows versions are supported?', 'WinMeter supports 64-bit Windows 10 and Windows 11. The taskbar strip is designed for a horizontal taskbar at the bottom of the screen.'],
  ['Why can’t I see a temperature reading?', 'Temperature appears only when Windows reports an ACPI thermal zone or a supported NVIDIA driver exposes GPU temperature. WinMeter does not install a kernel driver to read per-core CPU temperatures.'],
  ['Can I choose which metrics appear?', 'Yes. Open the home screen from the logo or by right-clicking the strip. You can toggle individual readings, change chart history between 1 minute, 10 minutes and 1 hour, and control startup with Windows.'],
];

function Logo({ size = 34 }) {
  return <img className="brand-logo" src={`${import.meta.env.BASE_URL}winmeter-logo.jpg`} width={size} height={size} alt="" />;
}

function DownloadButton({ className = '', version, compact = false }) {
  return (
    <a className={`button button-primary ${className}`} href={DOWNLOAD_URL}>
      <Download size={compact ? 17 : 19} strokeWidth={2} />
      <span>Download for Windows</span>
      {version && <span className="button-version">{version}</span>}
    </a>
  );
}

function Sparkline({ metric }) {
  return (
    <svg className="metric-chart" viewBox="0 0 408 100" role="img" aria-label={`${metric.label} example activity chart`} preserveAspectRatio="none">
      <defs>
        <linearGradient id={`fill-${metric.id}`} x1="0" y1="0" x2="0" y2="1">
          <stop offset="0%" stopColor={metric.color} stopOpacity="0.28" />
          <stop offset="100%" stopColor={metric.color} stopOpacity="0" />
        </linearGradient>
      </defs>
      {[25, 50, 75].map((y) => <line key={y} x1="0" y1={y} x2="408" y2={y} className="chart-grid" />)}
      <polygon points={`0,100 ${metric.points} 408,100`} fill={`url(#fill-${metric.id})`} />
      <polyline points={metric.points} fill="none" stroke={metric.color} strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round" vectorEffect="non-scaling-stroke" />
      <circle cx="408" cy={metric.points.split(' ').at(-1).split(',')[1]} r="4" fill={metric.color} />
    </svg>
  );
}

function App() {
  const [menuOpen, setMenuOpen] = useState(false);
  const [activeMetric, setActiveMetric] = useState('cpu');
  const [openFaq, setOpenFaq] = useState(0);
  const [version, setVersion] = useState('v0.3.0');
  const tabRefs = useRef([]);

  useEffect(() => {
    const controller = new AbortController();
    fetch('https://api.github.com/repos/imariman/win-meter/releases/latest', { signal: controller.signal })
      .then((response) => response.ok ? response.json() : Promise.reject(new Error('Release unavailable')))
      .then((release) => release.tag_name && setVersion(release.tag_name))
      .catch(() => {});
    return () => controller.abort();
  }, []);

  useEffect(() => {
    const onKeyDown = (event) => event.key === 'Escape' && setMenuOpen(false);
    window.addEventListener('keydown', onKeyDown);
    return () => window.removeEventListener('keydown', onKeyDown);
  }, []);

  useEffect(() => {
    const elements = document.querySelectorAll('[data-reveal]');
    if (window.matchMedia('(prefers-reduced-motion: reduce)').matches) {
      elements.forEach((element) => element.classList.add('is-visible'));
      return undefined;
    }
    const observer = new IntersectionObserver((entries) => {
      entries.forEach((entry) => {
        if (entry.isIntersecting) {
          entry.target.classList.add('is-visible');
          observer.unobserve(entry.target);
        }
      });
    }, { threshold: 0.12 });
    elements.forEach((element) => observer.observe(element));
    return () => observer.disconnect();
  }, []);

  const selectedMetric = metrics.find((metric) => metric.id === activeMetric);
  const closeMenu = () => setMenuOpen(false);
  const selectTabWithKeyboard = (event, index) => {
    if (!['ArrowRight', 'ArrowLeft', 'Home', 'End'].includes(event.key)) return;
    event.preventDefault();
    let next = index;
    if (event.key === 'ArrowRight') next = (index + 1) % metrics.length;
    if (event.key === 'ArrowLeft') next = (index - 1 + metrics.length) % metrics.length;
    if (event.key === 'Home') next = 0;
    if (event.key === 'End') next = metrics.length - 1;
    setActiveMetric(metrics[next].id);
    tabRefs.current[next]?.focus();
  };

  return (
    <>
      <a className="skip-link" href="#main">Skip to content</a>
      <header className="site-header">
        <div className="container nav-inner">
          <a className="brand" href="#top" aria-label="WinMeter home" onClick={closeMenu}>
            <Logo />
            <span>WinMeter</span>
          </a>
          <nav className={`nav-links ${menuOpen ? 'is-open' : ''}`} aria-label="Main navigation">
            <a href="#features" onClick={closeMenu}>Features</a>
            <a href="#how-it-works" onClick={closeMenu}>How it works</a>
            <a href="#faq" onClick={closeMenu}>FAQ</a>
            <a href={REPO_URL} target="_blank" rel="noreferrer" onClick={closeMenu}>GitHub <ExternalLink size={13} /></a>
          </nav>
          <div className="nav-actions">
            <a className="icon-button github-button" href={REPO_URL} target="_blank" rel="noreferrer" aria-label="WinMeter on GitHub"><Github size={20} /></a>
            <DownloadButton className="nav-download" compact />
            <button className="icon-button menu-button" type="button" aria-label={menuOpen ? 'Close menu' : 'Open menu'} aria-expanded={menuOpen} onClick={() => setMenuOpen(!menuOpen)}>
              {menuOpen ? <X size={22} /> : <Menu size={22} />}
            </button>
          </div>
        </div>
      </header>

      <main id="main">
        <section className="hero" id="top">
          <div className="hero-grid" aria-hidden="true" />
          <div className="hero-glow" aria-hidden="true" />
          <div className="container hero-content">
            <div className="eyebrow"><span className="status-dot" /> FREE &amp; OPEN SOURCE <span className="eyebrow-divider" /> MIT LICENSE</div>
            <h1>Your PC, at a glance.<br /><span>Right on your taskbar.</span></h1>
            <p className="hero-copy">A tiny system monitor for Windows. Keep CPU, memory, GPU, disk, network and more in sight — without getting in your way.</p>
            <div className="hero-actions">
              <DownloadButton version={version} />
              <a className="button button-secondary" href={REPO_URL} target="_blank" rel="noreferrer"><Github size={19} /> View on GitHub</a>
            </div>
            <p className="requirements"><Check size={14} /> Windows 10/11 <span>·</span> x64 <span>·</span> No installer <span>·</span> No admin rights</p>
          </div>
          <div className="container product-stage" data-reveal>
            <div className="stage-topbar">
              <div className="window-dots" aria-hidden="true"><span /><span /><span /></div>
              <span className="stage-label"><span className="status-dot" /> LIVE SYSTEM OVERVIEW</span>
              <span className="stage-version">{version}</span>
            </div>
            <div className="preview-wrap">
              <img src={`${import.meta.env.BASE_URL}preview.png`} alt="WinMeter taskbar strip, home dashboard and CPU detail screen with per-core load" width="1928" height="1600" fetchPriority="high" />
            </div>
          </div>
          <div className="container proof-strip" data-reveal>
            <div><strong>7</strong><span>live system metrics</span></div>
            <div><strong>1</strong><span>portable executable</span></div>
            <div><strong>0</strong><span>telemetry &amp; tracking</span></div>
            <div><strong>100%</strong><span>free &amp; open source</span></div>
          </div>
        </section>

        <section className="section feature-explorer" id="features">
          <div className="container">
            <div className="section-heading" data-reveal>
              <p className="section-kicker">EVERYTHING THAT MATTERS</p>
              <h2>A clearer view of your system.</h2>
              <p>From a quick glance to the smallest detail, the information you need is always one click away.</p>
            </div>
            <div className="metric-tabs" role="tablist" aria-label="System metrics" data-reveal>
              {metrics.map((metric, index) => {
                const Icon = metric.icon;
                return (
                  <button
                    key={metric.id}
                    ref={(element) => { tabRefs.current[index] = element; }}
                    className={`metric-tab ${activeMetric === metric.id ? 'is-active' : ''}`}
                    style={{ '--metric-color': metric.color }}
                    role="tab"
                    id={`tab-${metric.id}`}
                    aria-selected={activeMetric === metric.id}
                    aria-controls={`panel-${metric.id}`}
                    tabIndex={activeMetric === metric.id ? 0 : -1}
                    onClick={() => setActiveMetric(metric.id)}
                    onKeyDown={(event) => selectTabWithKeyboard(event, index)}
                  >
                    <Icon size={18} /><span>{metric.label}</span>
                  </button>
                );
              })}
            </div>
            <div className="metric-panel" role="tabpanel" id={`panel-${selectedMetric.id}`} aria-labelledby={`tab-${selectedMetric.id}`} key={selectedMetric.id}>
              <div className="metric-panel-copy">
                <span className="metric-overline" style={{ color: selectedMetric.color }}><selectedMetric.icon size={18} /> {selectedMetric.label} MONITORING</span>
                <h3>{selectedMetric.title}</h3>
                <p>{selectedMetric.description}</p>
                <a href={`${REPO_URL}#usage`} target="_blank" rel="noreferrer" className="text-link">Explore all capabilities <ArrowRight size={16} /></a>
              </div>
              <div className="metric-demo" style={{ '--metric-color': selectedMetric.color }}>
                <div className="demo-header">
                  <span><i style={{ background: selectedMetric.color }} />{selectedMetric.label}<small>Last minute</small></span>
                  <strong>{selectedMetric.value}</strong>
                </div>
                <Sparkline metric={selectedMetric} />
                <div className="demo-unit">{selectedMetric.unit}</div>
                <dl className="demo-details">
                  {selectedMetric.details.map(([label, value]) => <div key={label}><dt>{label}</dt><dd>{value}</dd></div>)}
                </dl>
              </div>
            </div>
          </div>
        </section>

        <section className="section bento-section">
          <div className="container">
            <div className="section-heading align-left" data-reveal>
              <p className="section-kicker">SMALL FOOTPRINT. BIG PICTURE.</p>
              <h2>Designed to stay out of your way.</h2>
            </div>
            <div className="bento-grid">
              <article className="feature-card feature-card-wide" data-reveal>
                <div className="feature-icon"><MousePointer2 size={22} /></div>
                <h3>One click to the details</h3>
                <p>Click any taskbar reading to open its detail screen. Switch between the last minute, 10 minutes or a full hour of history.</p>
                <div className="range-demo" aria-hidden="true"><span className="active">1 min</span><span>10 min</span><span>1 hour</span><div className="range-line"><i /></div></div>
              </article>
              <article className="feature-card" data-reveal>
                <div className="feature-icon green"><ShieldCheck size={22} /></div>
                <h3>Private by design</h3>
                <p>No accounts. No network calls. No telemetry. Your system data stays exactly where it belongs: on your PC.</p>
                <div className="privacy-pill"><span className="status-dot" /> Local only</div>
              </article>
              <article className="feature-card" data-reveal>
                <div className="feature-icon violet"><FileBox size={22} /></div>
                <h3>One file. That's it.</h3>
                <p>A self-contained executable with the .NET runtime bundled. Download, run and keep it wherever you like.</p>
                <div className="file-demo"><span className="file-icon"><ArrowDownToLine size={20} /></span><div><strong>WinMeter.exe</strong><small>Portable application · x64</small></div><Check size={18} /></div>
              </article>
              <article className="feature-card feature-card-wide" data-reveal>
                <div className="feature-icon amber"><Activity size={22} /></div>
                <h3>Warnings you can actually read</h3>
                <p>High readings turn amber, critical readings turn red. Clear “High” and “Critical” labels mean color is never the only signal.</p>
                <div className="warning-demo"><div><span><i className="blue-dot" /> CPU</span><strong>34%</strong></div><div className="warning-track"><i /></div><div><span><i className="green-dot" /> Memory <b>High</b></span><strong className="amber-text">81%</strong></div><div className="warning-track amber-track"><i /></div></div>
              </article>
            </div>
          </div>
        </section>

        <section className="section how-section" id="how-it-works">
          <div className="container">
            <div className="section-heading" data-reveal>
              <p className="section-kicker">UP AND RUNNING IN SECONDS</p>
              <h2>Less setup. More insight.</h2>
              <p>No installer, no configuration marathon. Just the essentials from the very first launch.</p>
            </div>
            <div className="steps-grid">
              <article className="step-card" data-reveal><span className="step-number">01</span><div className="step-icon"><Download size={24} /></div><h3>Download</h3><p>Get the latest self-contained executable for 64-bit Windows 10 or 11.</p></article>
              <article className="step-card" data-reveal><span className="step-number">02</span><div className="step-icon"><Zap size={24} /></div><h3>Run</h3><p>Open WinMeter.exe. CPU and memory appear beside your system clock automatically.</p></article>
              <article className="step-card" data-reveal><span className="step-number">03</span><div className="step-icon"><CircleGauge size={24} /></div><h3>Make it yours</h3><p>Choose your metrics and chart range. Enable “Start with Windows” if you want it always ready.</p></article>
            </div>
            <div className="command-card" data-reveal>
              <div><span className="command-icon"><Clock3 size={20} /></span><div><h3>One sample. One simple command.</h3><p>Useful for scripts, quick checks and bug reports.</p></div></div>
              <code><span>PS&gt;</span> WinMeter.exe --once</code>
            </div>
          </div>
        </section>

        <section className="section faq-section" id="faq">
          <div className="container faq-layout">
            <div className="faq-intro" data-reveal>
              <p className="section-kicker">GOOD TO KNOW</p>
              <h2>Questions,<br />answered.</h2>
              <p>Everything you need before your first launch.</p>
              <a className="text-link" href={`${REPO_URL}/issues`} target="_blank" rel="noreferrer">Still curious? Ask on GitHub <ArrowRight size={16} /></a>
            </div>
            <div className="faq-list" data-reveal>
              {faqs.map(([question, answer], index) => {
                const isOpen = openFaq === index;
                return (
                  <div className={`faq-item ${isOpen ? 'is-open' : ''}`} key={question}>
                    <h3>
                      <button type="button" aria-expanded={isOpen} aria-controls={`faq-answer-${index}`} onClick={() => setOpenFaq(isOpen ? -1 : index)}>
                        <span>{question}</span><ChevronDown size={20} />
                      </button>
                    </h3>
                    <div className="faq-answer" id={`faq-answer-${index}`} hidden={!isOpen}><p>{answer}</p></div>
                  </div>
                );
              })}
            </div>
          </div>
        </section>

        <section className="section final-cta-section">
          <div className="container">
            <div className="final-cta" data-reveal>
              <div className="cta-glow" aria-hidden="true" />
              <Logo size={54} />
              <p className="section-kicker">YOUR SYSTEM. YOUR DATA. YOUR TASKBAR.</p>
              <h2>Know what your PC is doing.</h2>
              <p>Free, open source and always a glance away.</p>
              <DownloadButton version={version} />
              <a className="release-link" href={RELEASES_URL} target="_blank" rel="noreferrer">View release notes <ExternalLink size={13} /></a>
            </div>
          </div>
        </section>
      </main>

      <footer className="site-footer">
        <div className="container footer-inner">
          <a className="brand" href="#top"><Logo size={28} /><span>WinMeter</span></a>
          <p>Free and open source. Built for Windows.</p>
          <div className="footer-links"><a href={`${REPO_URL}/blob/master/LICENSE`} target="_blank" rel="noreferrer">MIT License</a><a href={REPO_URL} target="_blank" rel="noreferrer" aria-label="GitHub repository"><Github size={19} /></a></div>
        </div>
        <div className="container footer-bottom"><span>© {new Date().getFullYear()} WinMeter</span><span>Made for people who like to know.</span></div>
      </footer>
    </>
  );
}

export default App;

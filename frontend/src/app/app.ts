import { Component, OnInit, AfterViewChecked } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

declare var lucide: any;

interface WatchlistEntry {
  query: string;
  targetPrice: number | null;
  enabled: boolean;
}

interface ProviderStatus {
  name: string;
  status: string;
  label: string;
}

interface BotStatus {
  isRunning: boolean;
  isScanning: boolean;
  uptime: string;
  lastScanAt: string | null;
  lastScanDate: string | null;
  mode: string;
  activeEntries: number;
  totalEntries: number;
  providers: ProviderStatus[];
}

interface Offer {
  id: string;
  title: string;
  price: number;
  originalPrice: number | null;
  provider: string;
  imageUrl: string;
  sourceUrl: string;
  affiliateLink: string | null;
}

interface Cluster {
  primaryTitle: string;
  cheapestOffer: Offer;
  offers: Offer[];
}

interface GeneratedOutput {
  id: string;
  title: string;
  price: string;
  provider: string;
  imageUrl: string;
  caption: string;
  generatedAt: string;
}

interface ParsedLog {
  time: string;
  level: string;
  category: string;
  message: string;
  levelClass: string;
}

@Component({
  selector: 'app-root',
  imports: [CommonModule, FormsModule],
  templateUrl: './app.html',
  styleUrl: './app.css'
})
export class App implements OnInit, AfterViewChecked {
  // Navigation tabs
  activeModule = 'watchlist'; // 'watchlist' | 'monitor' | 'results' | 'templates' | 'outputs' | 'settings'

  // Bot general status
  status: BotStatus = {
    isRunning: false,
    isScanning: false,
    uptime: '—',
    lastScanAt: null,
    lastScanDate: null,
    mode: 'both',
    activeEntries: 0,
    totalEntries: 0,
    providers: []
  };

  // Watchlist State
  watchlistEntries: WatchlistEntry[] = [];
  botMode = 'both';

  // Scan execution logs and steps
  logs: string[] = [];
  parsedLogs: ParsedLog[] = [];
  isScanning = false;
  scanStep = 0; // 0: Standby, 1: Scraping, 2: Comparing, 3: Rendering, 4: Publishing
  progressPercent = 0;
  progressStatus = 'Standby';

  // Compared Results
  clusters: Cluster[] = [];

  // Template Editor State
  activeTemplate = 'overlay-shopee'; // 'overlay-shopee' | 'carrossel-fotos' | 'videos-curtos'
  activeEditorTab = 'html'; // 'html' | 'css'
  templateCode = '';
  
  // Template mockup variables
  tTitle = 'Camisa Polo Classic Blue';
  tPrice = '89,90';
  tCategory = '12% OFF';
  tFooter = 'Preço médio de mercado: R$ 102,00';
  tImage = 'https://images.unsplash.com/photo-1521572267360-ee0c2909d518?w=300';

  // Generated outputs
  outputs: GeneratedOutput[] = [];

  // Settings
  shopeeId = '';
  shopeeSecret = '';
  shopeeTag = '';
  amazonTag = '';
  magaluStore = '';
  telegramToken = '';
  telegramChannel = '';

  private pollInterval: any = null;

  ngOnInit() {
    this.loadWatchlist();
    this.loadResults();
    this.loadLogs();
    this.updateStatus();
    
    // Core background polling every 3 seconds
    this.pollInterval = setInterval(() => {
      this.updateStatus();
      this.loadResults();
      if (!this.isScanning) {
        this.loadLogs();
      }
    }, 3000);
  }

  ngAfterViewChecked() {
    if (typeof lucide !== 'undefined') {
      lucide.createIcons();
    }
  }

  switchModule(module: string) {
    this.activeModule = module;
    if (module === 'watchlist') {
      this.loadWatchlist();
    } else if (module === 'results') {
      this.loadResults();
    } else if (module === 'templates') {
      this.loadTemplateCode();
    } else if (module === 'outputs') {
      this.loadOutputs();
    } else if (module === 'settings') {
      this.loadSettings();
    }
  }

  // --- API: Watchlist ---
  async loadWatchlist() {
    try {
      const res = await fetch('/api/watchlist');
      if (!res.ok) throw new Error();
      const config = await res.json();
      this.watchlistEntries = config.entries || [];
      this.botMode = config.mode || 'both';
    } catch {
      this.showToast('Erro ao carregar sementes da watchlist', 'error');
    }
  }

  addWatchlistEntry() {
    this.watchlistEntries.push({
      query: '',
      targetPrice: null,
      enabled: true
    });
  }

  deleteWatchlistEntry(idx: number) {
    this.watchlistEntries.splice(idx, 1);
  }

  async saveWatchlist() {
    const payload = {
      mode: this.botMode,
      entries: this.watchlistEntries.filter(e => e.query && e.query.trim() !== '')
    };

    try {
      const res = await fetch('/api/watchlist', {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(payload)
      });
      if (!res.ok) throw new Error();
      this.showToast('Watchlist salva com sucesso!', 'success');
      this.loadWatchlist();
    } catch {
      this.showToast('Falha ao salvar sementes', 'error');
    }
  }

  // --- API: Bot Status & Engine ---
  async updateStatus() {
    try {
      const res = await fetch('/api/status');
      if (!res.ok) throw new Error();
      const data = await res.json();
      this.status = data;

      if (data.isScanning) {
        if (!this.isScanning) {
          this.isScanning = true;
          // Quick poll logs when scanning
          this.loadLogs();
        }
      } else {
        if (this.isScanning) {
          this.isScanning = false;
          this.scanStep = 0;
          this.progressPercent = 0;
          this.progressStatus = 'Finalizado';
          this.loadLogs();
          this.loadResults();
          this.loadOutputs();
          this.showToast('Varredura finalizada!', 'success');
        }
      }
    } catch {
      this.status.isRunning = false;
    }
  }

  async triggerScan() {
    if (this.isScanning) return;
    this.isScanning = true;
    this.scanStep = 1;
    this.progressPercent = 10;
    this.progressStatus = 'Conectando com portais...';
    
    try {
      const res = await fetch('/api/scan/run', { method: 'POST' });
      if (!res.ok) throw new Error();
      this.showToast('Varredura em lote iniciada!', 'info');
      this.updateStatus();
    } catch {
      this.showToast('Falha ao iniciar varredura', 'error');
      this.isScanning = false;
    }
  }

  // --- API: Logs ---
  async loadLogs() {
    try {
      const res = await fetch('/api/logs');
      if (!res.ok) throw new Error();
      const data = await res.json();
      this.logs = data || [];
      this.parsedLogs = this.logs.map(line => this.parseLogLine(line));
      this.resolveVisualSteps(this.logs);
      
      // Auto scroll terminal body
      setTimeout(() => {
        const terminalBody = document.querySelector('.terminal-body');
        if (terminalBody) {
          terminalBody.scrollTop = terminalBody.scrollHeight;
        }
      }, 50);
    } catch (err) {
      console.error(err);
    }
  }

  async clearLogs() {
    try {
      await fetch('/api/logs', { method: 'DELETE' });
      this.logs = [];
      this.parsedLogs = [];
      this.scanStep = 0;
      this.progressPercent = 0;
      this.progressStatus = 'Standby';
    } catch {
      this.showToast('Erro ao limpar console', 'error');
    }
  }

  resolveVisualSteps(logs: string[]) {
    if (logs.length === 0) return;
    const logsText = logs.join('\n').toLowerCase();

    if (logsText.includes('telegram') || logsText.includes('publicado') || logsText.includes('canal')) {
      this.scanStep = 4;
      this.progressPercent = 95;
      this.progressStatus = 'Publicando anúncios...';
    } else if (logsText.includes('playwright') || logsText.includes('renderiz') || logsText.includes('criativo')) {
      this.scanStep = 3;
      this.progressPercent = 75;
      this.progressStatus = 'Renderizando anúncios...';
    } else if (logsText.includes('compar') || logsText.includes('cruz') || logsText.includes('calcul')) {
      this.scanStep = 2;
      this.progressPercent = 50;
      this.progressStatus = 'Calculando descontos...';
    } else if (logsText.includes('monitoramento') || logsText.includes('scout') || logsText.includes('busc')) {
      this.scanStep = 1;
      this.progressPercent = 25;
      this.progressStatus = 'Scraping portais...';
    }
  }

  parseLogLine(line: string): ParsedLog {
    let time = '00:00:00';
    let level = 'INF';
    let category = 'System';
    let message = line;

    const match = line.match(/^\[([\d:]+)\]\s+\[([A-Z]+)\]\s+\[([^\]]+)\]\s+(.*)$/);
    if (match) {
      time = match[1];
      level = match[2];
      category = match[3];
      message = match[4];
    }

    let levelClass = 'info';
    if (level === 'ERR') levelClass = 'error';
    else if (level === 'WRN') levelClass = 'warning';
    else if (level === 'SUCCESS' || message.toLowerCase().includes('sucesso') || message.toLowerCase().includes('concluído')) {
      levelClass = 'success';
    }

    return { time, level, category, message, levelClass };
  }

  // --- API: Results ---
  async loadResults() {
    try {
      const res = await fetch('/api/results');
      if (!res.ok) throw new Error();
      this.clusters = await res.json();
    } catch (err) {
      console.error(err);
    }
  }

  getClusterAveragePrice(cluster: Cluster): number {
    const prices = cluster.offers.map(o => o.price);
    if (prices.length === 0) return 0;
    return prices.reduce((sum, p) => sum + p, 0) / prices.length;
  }

  getClusterSavingsPercent(cluster: Cluster): number {
    const prices = cluster.offers.map(o => o.price);
    if (prices.length <= 1) return 0;
    const cheapest = Math.min(...prices);
    const mostExpensive = Math.max(...prices);
    if (mostExpensive === 0) return 0;
    return Math.round(((mostExpensive - cheapest) / mostExpensive) * 100);
  }

  getOffersBelowAverage(cluster: Cluster, average: number): Offer[] {
    if (cluster.offers.length <= 1) return [];
    return cluster.offers.filter(o => o.price < average);
  }

  getOffersRealDiscount(cluster: Cluster): Offer[] {
    return cluster.offers.filter(o => o.originalPrice && o.originalPrice > o.price);
  }

  getOffersOther(cluster: Cluster, average: number): Offer[] {
    const below = this.getOffersBelowAverage(cluster, average);
    const discount = this.getOffersRealDiscount(cluster);
    return cluster.offers.filter(o => !below.includes(o) && !discount.includes(o));
  }

  getOfferDifferenceText(price: number, average: number): { text: string; isBelow: boolean } {
    const diff = Math.abs(price - average);
    const pct = average > 0 ? Math.round((diff / average) * 100) : 0;
    const diffFormatted = diff.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' });
    if (price < average) {
      return { text: `-${diffFormatted} (${pct}% abaixo da média)`, isBelow: true };
    } else {
      return { text: `+${diffFormatted} (${pct}% acima da média)`, isBelow: false };
    }
  }

  // --- API: Templates Editor ---
  async loadTemplateCode() {
    try {
      const res = await fetch(`/api/templates/${this.activeTemplate}/${this.activeEditorTab}`);
      if (!res.ok) throw new Error();
      this.templateCode = await res.text();
      this.updateTemplatePreview();
    } catch {
      this.showToast('Erro ao carregar código do template', 'error');
    }
  }

  switchTemplate(tmpl: string) {
    this.activeTemplate = tmpl;
    this.loadTemplateCode();
  }

  switchEditorTab(tab: string) {
    this.activeEditorTab = tab;
    this.loadTemplateCode();
  }

  async updateTemplatePreview() {
    try {
      let htmlContent = '';
      let cssContent = '';

      if (this.activeEditorTab === 'html') {
        htmlContent = this.templateCode;
        const resCss = await fetch(`/api/templates/${this.activeTemplate}/css`);
        cssContent = await resCss.text();
      } else {
        cssContent = this.templateCode;
        const resHtml = await fetch(`/api/templates/${this.activeTemplate}/html`);
        htmlContent = await resHtml.text();
      }

      let renderedHtml = htmlContent
        .replaceAll('{{Product.Name}}', this.tTitle)
        .replaceAll('{{Product.Price}}', this.tPrice)
        .replaceAll('{{Product.Tag}}', this.tCategory)
        .replaceAll('{{Product.Footer}}', this.tFooter)
        .replaceAll('{{Product.Image}}', this.tImage);

      renderedHtml += `<style>${cssContent}</style>`;

      // Render template preview inside iframe safely
      const previewCard = document.getElementById('template-card-preview');
      if (previewCard) {
        let iframe = previewCard.querySelector('iframe') as HTMLIFrameElement;
        if (!iframe) {
          iframe = document.createElement('iframe');
          iframe.style.width = '100%';
          iframe.style.height = '100%';
          iframe.style.border = 'none';
          previewCard.innerHTML = '';
          previewCard.appendChild(iframe);
        }
        const doc = iframe.contentDocument || (iframe.contentWindow ? iframe.contentWindow.document : null);
        if (doc) {
          doc.open();
          doc.write(renderedHtml);
          doc.close();
        }
      }
    } catch (err) {
      console.error(err);
    }
  }

  async saveTemplate() {
    try {
      const res = await fetch(`/api/templates/${this.activeTemplate}/${this.activeEditorTab}`, {
        method: 'PUT',
        body: this.templateCode
      });
      if (!res.ok) throw new Error();
      this.showToast('Template salvo no disco!', 'success');
    } catch {
      this.showToast('Erro ao salvar template', 'error');
    }
  }

  // --- API: Outputs ---
  async loadOutputs() {
    try {
      const res = await fetch('/api/outputs');
      if (!res.ok) throw new Error();
      this.outputs = await res.json();
    } catch {
      this.showToast('Erro ao carregar criativos', 'error');
    }
  }

  copyCaption(caption: string) {
    const parser = new DOMParser();
    const doc = parser.parseFromString(caption, 'text/html');
    const decodedText = doc.documentElement.textContent || '';
    
    navigator.clipboard.writeText(decodedText).then(() => {
      this.showToast('Legenda copiada!', 'success');
    }).catch(() => {
      this.showToast('Falha ao copiar legenda', 'error');
    });
  }

  // --- API: Settings ---
  async loadSettings() {
    try {
      const res = await fetch('/api/settings');
      if (!res.ok) throw new Error();
      const data = await res.json();

      if (data.shopee) {
        this.shopeeId = data.shopee.appId || '';
        this.shopeeSecret = data.shopee.appSecret || '';
        this.shopeeTag = data.shopee.partnerTag || '';
      }
      if (data.amazon) {
        this.amazonTag = data.amazon.partnerTag || '';
      }
      if (data.magalu) {
        this.magaluStore = data.magalu.storeId || '';
      }
      if (data.telegram) {
        this.telegramToken = data.telegram.botToken || '';
        this.telegramChannel = data.telegram.channelId || '';
      }
    } catch {
      this.showToast('Falha ao obter configurações', 'error');
    }
  }

  async saveSettings() {
    const payload = {
      shopee: {
        appId: this.shopeeId.trim(),
        appSecret: this.shopeeSecret.trim(),
        partnerTag: this.shopeeTag.trim()
      },
      amazon: {
        partnerTag: this.amazonTag.trim()
      },
      magalu: {
        storeId: this.magaluStore.trim()
      },
      telegram: {
        botToken: this.telegramToken.trim(),
        channelId: this.telegramChannel.trim()
      }
    };

    try {
      const res = await fetch('/api/settings', {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(payload)
      });
      if (!res.ok) throw new Error();
      this.showToast('Configurações salvas com sucesso!', 'success');
    } catch {
      this.showToast('Erro ao salvar configurações', 'error');
    }
  }

  // --- Utility Toast ---
  showToast(message: string, type: 'success' | 'error' | 'info' = 'success') {
    const toast = document.createElement('div');
    toast.className = `toast toast--${type}`;
    let iconName = 'check-circle';
    if (type === 'error') iconName = 'alert-triangle';
    if (type === 'info') iconName = 'info';

    toast.innerHTML = `
      <i data-lucide="${iconName}"></i>
      <span>${message}</span>
    `;
    document.body.appendChild(toast);
    if (typeof lucide !== 'undefined') {
      lucide.createIcons({ attrs: { class: 'lucide-icon' } });
    }

    setTimeout(() => toast.classList.add('show'), 50);
    setTimeout(() => {
      toast.classList.remove('show');
      setTimeout(() => toast.remove(), 300);
    }, 3500);
  }
}

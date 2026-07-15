# Motor de Templates (Template Engine)

Este documento especifica a arquitetura e as diretrizes do motor de renderização HTML e a conversão de templates em saídas de mídia no **RZND Media**.

---

## Visão Geral do Motor de Renderização

O motor de templates do RZND Media utiliza tecnologias da web padrão (HTML, CSS e JavaScript) para estruturar as peças de mídia. A geração final é efetuada executando uma instância do navegador em modo headless (como Puppeteer) para capturar o estado renderizado em imagem (`.png`), vídeo (`.mp4`), GIF ou documento HTML.

Os templates ficam localizados sob a pasta `templates/`, organizados em duas subpastas de categorias padrão para suportar diferentes lógicas de saída:
1.  **`carrossel-fotos/` (Carrossel de Fotos):** Templates voltados para a geração de imagens estáticas ou conjuntos sequenciais de imagens que formam carrosséis de produtos para redes sociais.
2.  **`videos-curtos/` (Vídeos Curtos):** Templates que incluem animações CSS/Web Animations e transições temporais para renderização sequencial de quadros, gerando vídeos em formato curto como é feito no canva, com  movimento.

---

## Estrutura do Template HTML Base

Cada template é composto por uma pasta própria contendo:
*   `index.html` - Estrutura DOM e marcação do template.
*   `styles.css` - Estilização visual (obedecendo à identidade da marca).
*   `config.json` - Arquivo de configuração que define as variáveis aceitas pelo template, as instruções de captura (print ou vídeo) e as proporções (aspect ratio) suportadas de acordo com a plataforma de destino.

---

## Parâmetros de Captura e Adaptação de Proporção

O arquivo `config.json` atua como o manual de instruções para o motor de renderização headless. Ele especifica como capturar a mídia e como ajustar o viewport do navegador para diferentes plataformas de publicação:

### 1. Tipo de Captura (`captureType`)
Indica à engine se deve tirar uma captura estática da página ou gravar a tela em tempo de execução:
*   `screenshot`: A engine aguarda a renderização do DOM e captura uma imagem (ex: PNG/JPEG).
*   `video`: A engine aguarda o carregamento, inicia uma gravação contínua do viewport a uma taxa de quadros específica (ex: 30fps) pelo tempo de duração estipulado (ex: 15 segundos) para exportação para MP4.

### 2. Proporção e Resolução por Plataforma (`dimensions`)
Mapeia a resolução (Largura x Altura em pixels) e o comportamento do viewport dependendo do canal de publicação selecionado:

```json
{
  "templateId": "banner-promocional-01",
  "captureType": "screenshot",
  "defaultFormat": "png",
  "dimensions": {
    "local": { "width": 1080, "height": 1080, "aspectRatio": "1:1" },
    "instagram": { "width": 1080, "height": 1080, "aspectRatio": "1:1" },
    "telegram": { "width": 1280, "height": 720, "aspectRatio": "16:9" },
    "tiktok": { "width": 1080, "height": 1920, "aspectRatio": "9:16" }
  }
}
```

A engine de renderização ajusta dinamicamente a janela do navegador (*Viewport*) para as dimensões indicadas antes de capturar a mídia, garantindo que o template HTML/CSS seja flexível (responsivo) e se adapte às proporções exigidas por cada rede social.

---

## Sintaxe de Substituição e Placeholders

Placeholders nos arquivos HTML utilizam interpolação simples ou injeção de DOM controlada por seletores do motor de dados:
*   **Placeholder Simples:** `<div id="rznd-title">{{product.title}}</div>`
*   **Mapeamento Automatizado:** A engine mapeia chaves JSON do Provider de dados diretamente aos ids e classes identificados no template HTML.

---

## Motores de Renderização Suportados

A renderização é efetuada em lote no backend local através de:
*   **Puppeteer (Padrão):** Abre instâncias do Chromium em modo headless. Captura screenshots (`.png`) rápidas para carrosséis de fotos ou executa gravação de stream de tela para gerar arquivos de vídeo curtos (`.mp4`).
*   **Playwright (Alternativa):** Suporte opcional para ambientes corporativos com suporte a múltiplos navegadores.

---

## Otimizações de Renderização e Performance

Para garantir a geração em massa ágil localmente, a engine implementa:
*   **Reuso de Instância (Pool):** Uma única aba do navegador é reutilizada para renderizar múltiplos produtos consecutivamente, alterando as variáveis no DOM via injeção JavaScript, evitando o overhead de abrir e fechar abas/navegadores.
*   **Recursos Locais Pré-carregados:** Imagens comuns de template, fontes tipográficas e logotipos são cacheados localmente para eliminar latência de rede durante o lote.

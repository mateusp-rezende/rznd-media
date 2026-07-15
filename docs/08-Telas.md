# Interfaces e Telas (UI Mockups)

Este documento descreve a estrutura de telas e fluxos de navegação da interface do usuário do **RZND Media**, adotando a identidade visual estilo IDE profissional definida em [Identidade Visual](01-Identidade.md).

---

## Visão Geral da Interface do Usuário

O layout é dividido em três áreas de controle principais, inspiradas na disposição modular do VS Code. Abaixo está a representação esquemática do esqueleto global da interface:

```text
+---+-------------------+---------------------------------------------------+
| A | B. Explorer       | C. Workspace                                      |
| C |                   |                                                   |
| t | [Models]          | [aqui será exibido o conteúdo selecionado, e ações que poderão ser feitas]                               |
| i | > template_01     |                                                   |
| v | > template_02     |                                                   |
| i |                   |                                                   |
| t | [Providers]       |                                                   |
| y | > ERP_System      |                                                   |
|   | > CSV_Import      |                                                   |
| B |                   |                                                   |
| a | [Publishers]      |                                                   |
| r | > Instagram_API   |                                                   |
|   | > Telegram_Channel|                                                   |
+---+-------------------+---------------------------------------------------+
| Status Bar (Log / Versão / Conexão)                                       |
+---------------------------------------------------------------------------+
```

*   **A. Activity Bar (Barra de Atividades):** Extrema esquerda, compacta, contendo ícones (SVG) para navegação rápida de seções.
*   **B. Context Explorer (Painel de Contexto):** Exibe árvores de arquivos, listas de templates locais, conexões ativas ou registros de logs em andamento.
*   **C. Main Workspace (Área de Trabalho Principal):** Área central expandida onde as interações, parametrizações e exibições visuais ocorrem.
*   **Status Bar (Barra de Status):** Rodapé contendo informações sobre o ambiente ativo (Local/Offline), status de carregamento, e mensagens rápidas de sucesso ou erro.

---

## Painel Principal (Dashboard)

O Dashboard fornece uma visão rápida sobre o volume de mídia gerada, templates ativos e conexões em funcionamento.

### Seções do Dashboard:
1.  **Métricas Rápidas:**
    *   Painéis técnicos sem sombras, exibindo texto com alto contraste e contadores (ex: "Total de Mídias Geradas", "Publishers Ativos", "Templates Instalados").
2.  **Fila de Processamento Local:**
    *   Lista compacta em tabela mostrando processos ativos de renderização em massa (ex: geração de 100 imagens de produtos a partir de um lote de planilha).
3.  **Logs do Servidor Interno:**
    *   Painel inferior tipo console para monitoramento em tempo real das atividades locais de backend.

---

## Construtor e Editor de Templates

Uma interface dividida (*split-view*) voltada para a edição, configuração de parâmetros e pré-visualização instantânea da renderização dos templates.

### Painéis de Trabalho:
*   **Painel Esquerdo (Configurações do Template):** Campos de input discretos para mapeamento de variáveis do template (ex: Títulos, Preços, Fontes, Logotipos).
*   **Painel Central (Editor/Visualizador de Código):** Área mono-espaçada focada no código HTML/CSS do template.
*   **Painel Direito (Live Preview):** Visualização em tempo real da renderização final do template baseada nas variáveis de simulação inseridas.
*   **Controles Rápidos (Toolbar superior):** Botões compactos com ícones SVG para: *Executar Geração Única*, *Iniciar Processamento em Massa* e *Exportar HTML*.

---

## Gerenciamento de Providers e Publishers

Espaço para a integração e teste de conexões com fontes de dados de entrada e destinos de saída de mídia.

### Comportamento de Navegação:
*   **Seleção no Explorer:** Ao selecionar um item específico no painel lateral de contexto (ex: clicar em `CSV_Import` ou `ERP_System` dentro da lista de *Providers*), a Área de Trabalho Principal (*Workspace*) atualiza-se para exibir todo o conteúdo correspondente daquela fonte de dados.
*   **Conteúdo no Workspace:** Exibição detalhada dos produtos e dados obtidos da fonte selecionada:
    *   **Painel de Configuração:** Campos de parâmetros, chaves de autenticação e status da conexão.
    *   **Visualização de Produtos (Para Bancos de Dados / SQL):** Disponibiliza uma grade interativa que lista os produtos existentes e fornece uma interface de **CRUD completo** (Criação, Leitura, Atualização e Deleção) para manipulação dos registros direto no banco.
    *   **Visualização de Arquivos (Para Excel, CSV, JSON):** Exibe a pré-visualização das linhas de dados brutas e disponibiliza um botão de ação rápida para redirecionar o usuário ou abrir o arquivo correspondente localmente no sistema operacional para edição manual externa.

### Componentes Principais da Tela:
*   **Explorer de Provedores (Providers Explorer):** Exibe a árvore com todas as fontes de dados ativas ou criadas no painel de contexto lateral.
*   **Mapeador de Chaves (Data Mapper):** Exibido no workspace ao abrir um provider, permitindo associar colunas ou propriedades da fonte de dados aos placeholders do template HTML.
*   **Explorer de Publicadores (Publishers Explorer):** Exibe no painel de contexto os destinos configurados (redes sociais, diretórios, APIs), abrindo no workspace suas respectivas chaves de API, status de envio e fila de publicações ativas ao serem clicados.

---

## Tela de Configurações Gerais

Área dedicada a preferências da aplicação e parâmetros de renderização local.

### Configurações Disponíveis:
*   **Tema da Interface:** Botão de seleção simples para alternar entre *Tema Escuro (Dark)* e *Tema Claro (Light)*.
*   **Motor de Renderização:** Escolha do headless browser padrão (ex: Puppeteer integrado ou Playwright).
*   **Diretório de Output:** Input para indicar o caminho físico absoluto onde as mídias geradas serão gravadas localmente.
*   **Limites de Processamento:** Definições de taxa máxima de renderização simultânea para evitar consumo excessivo de hardware local.

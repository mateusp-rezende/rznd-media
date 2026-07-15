# Interfaces e Telas (UI Mockups)

Este documento descreve a estrutura de telas e fluxos de navegação da interface do usuário do **RZND Media**, adotando a identidade visual estilo IDE profissional definida em [Identidade Visual](01-Identidade.md).

---

## Visão Geral da Interface do Usuário

O layout é dividido em três áreas de controle principais, inspiradas na disposição modular do VS Code. Abaixo está a representação esquemática do esqueleto global da interface:

```text
+---+-------------------+---------------------------------------------------+
| A | B. Explorer       | C. Workspace                                      |
| C |                   |                                                   |
| t | [Templates]       | [aqui será exibido o conteúdo selecionado, e ações que poderão ser feitas]                               |
| i | v carrossel-fotos |                                                   |
| v |   > banner-prod   |                                                   |
| i | v videos-curtos   |                                                   |
| t |   > reels-promo   |                                                   |
|   | [Providers]       |                                                   |
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

## Monitor de Lote (Dashboard Principal)

O Dashboard Principal foi redesenhado como um **Monitor de Lote (Batch Monitor)** focado em automação em massa e controle IDE-like do runtime. Ele possui um layout de 3 colunas de alta densidade no workspace central:

1.  **Coluna 1 (Esquerda): Configuração / Job Profile**
    *   **Seletor de Fonte de Dados:** Permite alternar entre o banco local (SQLite zero-setup), planilhas CSV, APIs externas ou o Bot Crawler de promoções.
    *   **Seletor de Template de Saída:** Associa o Job ao template HTML/CSS ativo.
    *   **Mapeamento de Variáveis (De/Para):** Interface visual para conectar as propriedades do template (`{{Name}}`, `{{Price}}`, `{{Tag}}`, `{{Footer}}`, `{{Image}}`) com as colunas da fonte de dados correspondente (com suporte a mapeamento automático inteligente).
    *   **Diretório de Saída (Folders):** Opção de categorizar mídias geradas em pastas específicas (ex: `/produtos-eletronicos/`, `/produtos-academia/`) com base nos bots que alimentam a base.
    *   **Automação Contínua (Switch):** Se ativado, ouve atualizações nas fontes de dados e executa o processamento em lote em background de forma totalmente automática.
    *   **Salvar Job Profile:** Botão para gravar as configurações do Job em disco.
    *   **Debug Manual Drawer:** Painel retrátil para testar renderizações rápidas individuais.

2.  **Coluna 2 (Centro): Execução / Console Runtime**
    *   **Botão Executar Lote:** Disparador premium em destaque para iniciar o processo de compilação em massa.
    *   **Barra de Progresso Dinâmica:** Exibe status detalhado do worker e progresso percentual.
    *   **Console Terminal Logs:** Tela de console com fonte monospace estilo VS Code, exibindo em tempo real logs detalhados do Worker (.NET Core Background Service), incluindo chamadas Puppeteer headless, viewports renderizadas, gravações de arquivos e erros gerados.

3.  **Coluna 3 (Direita): Resultados da Execução**
    *   **Contador de Resultados:** Exibe número total de itens gerados.
    *   **Segmented Control de Filtros:** Abas de atalho rápido para filtrar itens: `[ Todos ]`, `[ Sucesso ]`, `[ Com Erro ]`.
    *   **Lista de Resultados Interativa:** Lista os arquivos gerados contendo o status. Em caso de erro (ex: falhas de imagens ou preços nulos), a linha exibe a mensagem de falha em vermelho e um botão **Corrigir & Rodar (Retry)** para re-executar especificamente aquele item com erro sem processar o lote inteiro novamente.

---

## Centro de Controle e Escala (Power User Mode)

Uma interface unificada dividida em dois painéis fixos que trabalham em conjunto, garantindo velocidade de mapeamento de dados e controle absoluto do código.

### Painéis de Trabalho:
*   **Painel Esquerdo: [Data & Mapping] (Conexão e Escala)**
    *   **Seletores de Modo:** Abas superiores para alternar entre **Edição Manual** (formulário rápido para preenchimento de variáveis únicas) e **Conexão de Dados** (para ligar provedores externos como SQL Server, planilhas CSV, ou APIs).
    *   **Mapeador de Variáveis (Mapping De/Para):** Vincula as propriedades do template HTML (ex: `{{Product.Name}}`) às colunas detectadas na fonte de dados (ex: `price_final` da API). Possui indicador de **Conexão Automática** se os nomes dos campos forem idênticos.
    *   **Pré-visualização de Lote:** Exibe as primeiras linhas da tabela mapeada para o usuário validar as substituições antes de processar em massa.
    *   **Configurações do Catálogo HTML (B2C):** Controles especiais para gerar um site de catálogo completo e estático (estilo Shopee) com inputs para configurar o link de redirecionamento de WhatsApp ou Checkout e a estrutura de diretórios de agrupamento (pastas por Categoria/Tag ou pasta única Flat). Inclui o botão **Gerar & Exportar Catálogo HTML**.
    *   **Botão "Executar Lote":** Dispara o motor de processamento em massa em segundo plano com barra de progresso visual.
*   **Painel Direito: [Live Preview & Engine]**
    *   **Live Preview:** Exibe em tempo real o template HTML/CSS renderizado com os dados mapeados.
    *   **Botão Fixo "Editar Template (Código)":** Posicionado na barra de ferramentas superior. Ao ser clicado, expande uma janela lateral com um editor de código (estilo VS Code / Monaco Editor) contendo abas para `index.html` e `styles.css` ao lado do preview.
    *   **Controle de Viewport:** Segmented control para definir a proporção do viewport headless (Local 1:1, Instagram 1:1, Telegram 16:9, TikTok 9:16).
    *   **Ações Individuais e Dropdown de Lote:** Botões para Capturar PNG, Gravar MP4 e um dropdown "Exportar Lote" (Salvar Local, Enviar Telegram, Agendar Instagram, Exportar Catálogo HTML) aplicável a todas as mídias geradas no processamento.

---

## Gerenciamento de Providers e Publishers

Espaço para a integração e teste de conexões com fontes de dados de entrada e destinos de saída de mídia.

### Comportamento de Navegação:
*   **Seleção no Explorer:** Ao selecionar um item específico no painel lateral de contexto (ex: clicar em `CSV_Import` ou `ERP_System` dentro da lista de *Providers*), a Área de Trabalho Principal (*Workspace*) atualiza-se para exibir todo o conteúdo correspondente daquela fonte de dados.
*   **Conteúdo no Workspace:** Exibição detalhada dos produtos e dados obtidos da fonte selecionada:
    *   **Painel de Conexão Local (Zero-Setup):** O banco de dados local (ex: SQLite) já vem configurado. Ao abrir a aplicação em `localhost`, a conexão é ativa de imediato, sem exibir campos de configuração de credenciais na interface.
    *   **Painel de Configuração (Opcional - APIs e Bots):** Campos de parâmetros de conexão (ex: endpoint da API, chaves de autenticação ou palavras-chave de busca do Bot de Promoção) só estarão visíveis e editáveis caso o lojista selecione uma integração via API ou Bot (ambos também já vêm pré-configurados por padrão, necessitando apenas de eventuais ajustes pontuais do usuário).
    *   **Visualização de Produtos (Para o Banco de Dados Local / SQL):** Grade interativa para realizar as operações de **CRUD completo** diretamente no banco de dados integrado local.
    *   **Visualização de Arquivos (Para Excel, CSV, JSON):** Exibe a pré-visualização das linhas de dados brutas e disponibiliza um botão de ação rápida para abrir e editar o arquivo no respectivo editor externo nativo do sistema operacional.

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

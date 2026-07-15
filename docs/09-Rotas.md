# Mapeamento de Rotas

Este documento detalha o mapeamento das rotas de navegação da aplicação cliente (Frontend) e os caminhos de comunicação do servidor local (Backend API) da plataforma **RZND Media**.

---

## Rotas de Navegação (Frontend)

As rotas da interface seguem a estrutura de painéis da barra de atividades (Activity Bar), atualizando a Área de Trabalho Principal (*Workspace*) com base no módulo ativo:

| Rota | Componente de Workspace | Painel de Contexto (Sidebar) | Descrição |
| :--- | :--- | :--- | :--- |
| `/` | Dashboard | Fila de Geração e Logs | Visão geral da plataforma e tarefas ativas. |
| `/templates` | Editor HTML & Live Preview | Árvore de Templates Locais | Edição de templates e customização de variáveis. |
| `/providers` | Tabela de Produtos (CRUD) / Configurações | Lista de Provedores | Gerenciamento de conexões SQL e arquivos Excel/CSV. |
| `/publishers` | Configuração de Chaves & Fila | Lista de Publicadores | Configuração de destinos e histórico de publicações. |
| `/settings` | Configurações do Sistema | Opções do Sistema | Definição de tema (Claro/Escuro) e caminhos de pastas. |

---

## Endpoints de API (Backend/Local Server)

Os endpoints do servidor local são consumidos pelo frontend para gerenciar recursos locais, interagir com bancos SQL e disparar comandos do sistema operacional para arquivos locais.

### 1. API de Providers e Produtos
*   `GET /api/providers` - Lista todos os provedores registrados.
*   `GET /api/providers/:id` - Retorna as configurações e status de conexão de um provedor específico.
*   `POST /api/providers/:id/test` - Testa a conectividade com o banco ou valida a presença do arquivo no diretório.
*   `GET /api/providers/:id/products` - Retorna a lista de produtos (caso SQL: lê do banco; caso arquivo: faz parse do Excel/CSV).
*   `POST /api/providers/:id/products` - Adiciona novo produto (somente para provedores SQL).
*   `PUT /api/providers/:id/products/:productId` - Atualiza dados do produto (somente para provedores SQL).
*   `DELETE /api/providers/:id/products/:productId` - Remove produto (somente para provedores SQL).
*   `POST /api/providers/:id/open-file` - Dispara comando do sistema operacional para abrir o arquivo de planilha correspondente (Excel/LibreOffice) no editor local do usuário.

### 2. API de Templates
*   `GET /api/templates` - Lista os templates locais.
*   `POST /api/templates/render` - Executa a renderização do template em lote ou unitário, convertendo HTML para imagem/PDF/vídeo.

---

## Padrões de Segurança e Middleware de Rotas

*   **Validação de Caminhos de Arquivo:** O servidor local valida que qualquer caminho de arquivo acessado está estritamente contido nos diretórios autorizados do usuário, evitando vulnerabilidades de travessia de diretório (Directory Traversal).
*   **Controle de Acesso Local:** Os endpoints do backend local são protegidos por tokens de sessão efêmeros gerados no ciclo de inicialização da plataforma para assegurar que apenas a interface do usuário local faça requisições ao servidor do RZND Media.

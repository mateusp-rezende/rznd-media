# Integração com Fontes de Dados (Providers)

Este documento especifica a arquitetura, as interfaces e os comportamentos necessários para implementar e integrar novas fontes de dados (Providers) à plataforma **RZND Media**.

---

## Visão Geral dos Providers

Os **Providers** são os módulos responsáveis por alimentar a plataforma com informações de produtos. Eles extraem dados de fontes externas (bancos de dados, arquivos locais, APIs de ERPs) e os convertem para um formato comum padronizado legível pelo motor de renderização.

A plataforma suporta dois fluxos operacionais principais dependendo do tipo do provedor:
1.  **Provedores Dinâmicos/SQL:** Oferecem conexão bidirecional, permitindo a leitura e a escrita direta dos dados através de uma interface CRUD.
2.  **Provedores de Arquivo Local:** Permitem a leitura estruturada de planilhas e arquivos locais, com ações rápidas para abrir e editar os arquivos diretamente no sistema operacional.

---

## Interface e Assinatura Base de um Provider

Todos os Providers devem implementar a interface/contrato unificado do sistema. Em pseudocódigo técnico:

```typescript
interface IDataProvider {
  id: string;
  name: string;
  type: 'sql' | 'file' | 'api';
  
  // Conexão e Validação
  testConnection(): Promise<boolean>;
  
  // Recuperação de Dados
  getProducts(): Promise<Product[]>;
  getProductById(id: string): Promise<Product>;
  
  // Persistência (opcional, aplicável a tipo 'sql' e APIs editáveis)
  createProduct?(product: Omit<Product, 'id'>): Promise<Product>;
  updateProduct?(id: string, product: Partial<Product>): Promise<boolean>;
  deleteProduct?(id: string): Promise<boolean>;
  
  // Comportamento específico de arquivos
  getLocalFilePath?(): string;
  openLocalFile?(): void;
}
```

---

## Tipos de Provedores de Dados

### 1. Banco de Dados / SQL
Permite a integração com bancos de dados relacionais (PostgreSQL, MySQL, SQL Server, SQLite).
*   **Comportamento:** O usuário insere a string de conexão na interface da IDE. A plataforma carrega as tabelas e expõe as operações CRUD diretamente na interface visual.
*   **Integração:** As alterações efetuadas na tabela de produtos dentro do *Workspace* do RZND Media são persistidas em tempo real no banco configurado.

### 2. Arquivos Locais (Excel, CSV, JSON)
Permite carregar listas de produtos armazenadas localmente.
*   **Comportamento:** O usuário seleciona o caminho absoluto do arquivo no disco. O RZND Media realiza o parse e renderiza os registros em formato de tabela no *Workspace*.
*   **Sincronização:** Por ser um arquivo local, a edição direta do conteúdo é feita no software padrão do sistema operacional (ex: Excel). A IDE do RZND Media disponibiliza um botão de atalho rápido que redireciona e abre o arquivo no respectivo editor nativo e recarrega os dados automaticamente após o salvamento.

### 3. APIs REST / ERPs
Permite ler dados diretamente de sistemas de gestão legados integrados.

---

## Configuração e Registro de um Novo Provider

Os provedores são carregados dinamicamente via registro de metadados no arquivo de configurações globais da plataforma ou através da pasta `providers/` do projeto.

Para registrar um novo provider, deve-se criar um manifesto JSON correspondente:

```json
{
  "providerId": "rznd-provider-postgres-local",
  "displayName": "PostgreSQL Local",
  "entryPoint": "dist/index.js",
  "configFields": [
    { "name": "connectionString", "type": "password", "required": true },
    { "name": "tableName", "type": "text", "required": true }
  ]
}
```

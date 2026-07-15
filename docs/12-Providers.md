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

### 1. Banco de Dados / SQL Local (Pre-configurado)
O sistema vem pre-configurado de fábrica com um banco de dados local (ex: SQLite ou banco embarcado).
*   **Comportamento Zero-Setup:** Ao clonar/baixar o repositório do GitHub e rodar a aplicação, a conexão com o banco local é estabelecida de forma transparente. A aplicação web inicia em `localhost` pronta para uso imediato, sem necessidade de inserir credenciais ou strings de conexão complexas.
*   **Gestão de Dados Interna:** O usuário cadastra, lê, atualiza e exclui (CRUD) os produtos de seu catálogo diretamente pela interface visual do RZND Media.
*   **Persistência:** Quaisquer alterações feitas na grade de produtos são persistidas no banco local e alimentam o motor de lote no mesmo instante.
*  **modelos html** já vem no sistema modelos padrões organização de pastas e etc.


### 2. Arquivos Locais (Excel, CSV, JSON)
Permite carregar listas adicionais de produtos armazenadas localmente no disco.
*   **Sincronização:** Por ser um arquivo local, a IDE do RZND Media disponibiliza um botão de atalho rápido que abre o arquivo no respectivo editor nativo do sistema operacional e recarrega os dados após o salvamento.

### 3. APIs REST / ERPs / Bot de Promoções (Pré-configurados)
Permite conectar-se de forma complementar a sistemas de terceiros ou bots de escuta de promoções.
*   **Comportamento:** O sistema traz integrações prontas por padrão. Os campos de configurações de chaves de API, endpoints ou palavras-chave de busca do bot só aparecem na tela quando o provedor correspondente é selecionado, servindo para ajustes finos do usuário.

---

## Configuração e Registro de um Novo Provider (Se aplicável)

Para fontes customizadas de terceiros (como novas APIs ou ERPs), os provedores são declarados via manifesto JSON correspondente definindo campos que aparecerão na UI para edição:

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

# Especificação da API

Este documento detalha as especificações técnicas da API local da plataforma **RZND Media**, detalhando os payloads de envio, respostas JSON e comportamentos de erros.

---

## Visão Geral da API

A API é exposta localmente (geralmente sob `http://localhost:port`) utilizando o protocolo HTTP/JSON. Ela atua como ponte de comunicação entre a interface visual (IDE) e as operações locais do sistema operacional (acesso a arquivos, consultas a banco de dados e execução do motor de renderização).

---

## Endpoints de Geração de Mídia

### 1. Iniciar Renderização em Lote
Dispara o motor de renderização para processar um conjunto de dados e gerar os arquivos finais.

*   **URL:** `/api/templates/render`
*   **Método:** `POST`
*   **Corpo da Requisição (Payload):**
    ```json
    {
      "templateId": "banner-produto-promocao",
      "providerId": "csv-produtos-promocionais",
      "format": "png",
      "resolution": {
        "width": 1080,
        "height": 1080
      },
      "outputPath": "C:/Repos/rznd-media/output",
      "limit": 50
    }
    ```
*   **Resposta de Sucesso (202 Accepted):**
    ```json
    {
      "jobId": "job_9823749823",
      "status": "processing",
      "totalItems": 42,
      "message": "Renderização em lote iniciada localmente."
    }
    ```

---

## Endpoints de Gerenciamento de Conteúdo (CRUD)

Estes endpoints realizam operações de banco de dados diretamente através de conexões SQL configuradas no Provider ativo.

### 1. Criar Produto (Somente SQL Providers)
*   **URL:** `/api/providers/:id/products`
*   **Método:** `POST`
*   **Corpo da Requisição (Payload):**
    ```json
    {
      "title": "Camisa Polo Classic Blue",
      "price": 89.90,
      "imageUrl": "https://cdn.exemplo.com/camisa.jpg",
      "sku": "POLO-BL-01",
      "description": "Camisa polo 100% algodão cor azul marinho."
    }
    ```
*   **Resposta de Sucesso (210 Created):**
    ```json
    {
      "success": true,
      "productId": "982",
      "data": {
        "id": "982",
        "title": "Camisa Polo Classic Blue",
        "price": 89.90,
        "sku": "POLO-BL-01"
      }
    }
    ```

### 2. Atualizar Produto (Somente SQL Providers)
*   **URL:** `/api/providers/:id/products/:productId`
*   **Método:** `PUT`
*   **Corpo da Requisição (Payload):**
    ```json
    {
      "price": 79.90
    }
    ```
*   **Resposta de Sucesso (200 OK):**
    ```json
    {
      "success": true,
      "message": "Produto 982 atualizado com sucesso."
    }
    ```

---

## Endpoints de Integrações (Providers & Publishers)

### 1. Abrir Arquivo de Planilha Local (Excel/CSV)
Solicita ao servidor local que acione o utilitário do sistema operacional para abrir o arquivo vinculado no editor padrão do usuário.

*   **URL:** `/api/providers/:id/open-file`
*   **Método:** `POST`
*   **Resposta de Sucesso (200 OK):**
    ```json
    {
      "success": true,
      "filePath": "C:/Planilhas/produtos.xlsx",
      "message": "Arquivo aberto no editor local padrão do sistema operacional."
    }
    ```

---

## Tratamento de Erros e Códigos de Retorno

A API do RZND Media utiliza códigos HTTP padronizados acompanhados de uma estrutura de erro consistente:

### Estrutura de Resposta de Erro
```json
{
  "error": {
    "code": "PROVIDER_CONNECTION_FAILED",
    "message": "Não foi possível estabelecer conexão com o banco de dados SQL especificado.",
    "details": "Timeout ao tentar conectar ao host 127.0.0.1:5432."
  }
}
```

### Códigos de Status Comuns:
*   **400 Bad Request:** Payload malformado, parâmetros inválidos ou tentativa de CRUD em provedores de arquivo local (somente leitura).
*   **404 Not Found:** Recurso (template, provider ou produto) não localizado.
*   **500 Internal Server Error:** Ocorreu uma falha não tratada ao renderizar a mídia ou acessar o arquivo.

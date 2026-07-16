# Configurações do Sistema

Este documento descreve as variáveis de ambiente, arquivos de configuração global e opções de parametrização da plataforma **RZND Media**.

## Configurações do Servidor Local (`appsettings.json`)

O arquivo [appsettings.json](file:///c:/Repos/rznd-media/src/Rznd.Bot/appsettings.json) na raiz do executável armazena as chaves de API, URLs bases e controle de logs do Serilog. A estrutura principal inclui:

```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information"
    },
    "WriteTo": [
      { "Name": "Console" },
      { "Name": "File", "Args": { "path": "logs\\RZND.Bot-.log" } }
    ]
  },
  "Shopee": {
    "AppId": "",
    "AppSecret": "",
    "BaseUrl": "https://open-api.affiliate.shopee.com.br/",
    "PartnerTag": ""
  },
  "Amazon": {
    "PartnerTag": "",
    "BaseUrl": "https://www.amazon.com.br/"
  },
  "Magalu": {
    "StoreId": "",
    "BaseUrl": "https://www.magazineluiza.com.br/"
  },
  "Urls": "http://localhost:5100"
}
```

- **Urls**: Define o host e a porta sob os quais a Minimal API do Kestrel e o painel web estarão disponíveis localmente (padrão: `http://localhost:5100`).
- **Shopee/Amazon/Magalu**: Credenciais necessárias para alternar as buscas do modo simulação para o modo real.

## Configurações da Lista de Monitoramento (`watchlist.json`)

O arquivo `watchlist.json` define quais palavras-chave e limites o bot rastreará durante o monitoramento no modo Watchlist.

```json
{
  "mode": "both",
  "entries": [
    {
      "keyword": "Smartphone Samsung Galaxy S25",
      "maxPrice": null,
      "enabled": true
    },
    {
      "keyword": "Notebook Dell Inspiron 15",
      "maxPrice": 4500,
      "enabled": true
    }
  ]
}
```

### Parâmetros:
- **`mode`**: Define a estratégia ativa de captação de sementes. Aceita os valores:
  - `"trend"`: Captura apenas produtos em alta nos feeds automáticos.
  - `"watchlist"`: Busca apenas os termos do array de `entries`.
  - `"both"`: Executa ambas as estratégias em paralelo.
- **`keyword`**: O termo exato enviado às APIs de busca das lojas.
- **`maxPrice`**: Filtro de preço. Se o menor preço for superior a este limite, o produto é ignorado no ciclo. Aceita valores decimais ou `null` (sem limite).
- **`enabled`**: Booleano (`true`/`false`) para ativar/desativar temporariamente o monitoramento do termo sem precisar excluí-lo da lista.

## Recarga Dinâmica (Hot-Reload)

O módulo `WatchlistLoader` utiliza um `FileSystemWatcher` para monitorar alterações no `watchlist.json`. Sempre que a lista é alterada via painel (ou por edição manual direta no arquivo), os novos parâmetros são recarregados dinamicamente na memória da aplicação sem a necessidade de reiniciar o bot.

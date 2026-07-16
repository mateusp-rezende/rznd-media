# Instalação e Inicialização

Este documento orienta o desenvolvedor sobre como preparar o ambiente local, instalar dependências e inicializar a plataforma **RZND Media**.

## Pré-requisitos

Para rodar a plataforma localmente, certifique-se de que sua máquina possui as seguintes ferramentas:
1. **.NET SDK 10.0 ou posterior**: Ambiente de execução principal e compilador para o C#.
2. **Navegador Web Moderno**: Chrome, Firefox, Edge ou Safari (para acessar o dashboard local).

## Instalação de Dependências

O bot utiliza pacotes NuGet oficiais para processamento e logging.
1. Abra um terminal na raiz do projeto `c:\Repos\rznd-media`.
2. Restaure os pacotes NuGet executando:
   ```bash
   dotnet restore
   ```
   *Os pacotes chave instalados automaticamente incluem:*
   - `FuzzySharp`: Algoritmo rápido de comparação de strings (Levenshtein).
   - `HtmlAgilityPack`: Parser de HTML leve para os WebScrapers da Amazon e Magalu.
   - `Serilog`: Motor de logs integrado estruturado com sinks para console e arquivo.

## Inicialização do Servidor Local (Dev Mode)

A aplicação é compilada e executada diretamente por comandos de terminal .NET.
1. Navegue até a pasta do projeto ou simplesmente execute a partir da raiz:
   ```bash
   dotnet run --project src/Rznd.Bot/RZND.Bot.csproj
   ```
2. O Kestrel iniciará a Minimal API e começará a escutar requisições de rede.
3. Abra seu navegador em: **`http://localhost:5100`**.

## Build e Produção

Para gerar os binários compilados otimizados para produção:
```bash
dotnet publish src/Rznd.Bot/RZND.Bot.csproj -c Release -o ./publish
```
Este comando criará um executável autônomo na pasta `./publish`, contendo a API do servidor e a pasta `wwwroot/` de arquivos estáticos.

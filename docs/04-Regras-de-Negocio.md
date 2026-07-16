# Regras de Negócio

Este documento descreve as regras operacionais, os critérios de validação e as diretrizes de processamento de dados da plataforma **RZND Media**.

## Validação e Normalização de Produtos

### 1. Limpeza de Título (TitleNormalizer)
Antes de realizar pesquisas ou comparações fuzzy, os títulos dos produtos capturados passam por uma sanitização rigorosa para remover "ruídos de marketing" que atrapalham os algoritmos de busca:
- **Termos de marketing removidos**: "Promoção", "Oferta", "Frete Grátis", "Lançamento", "Original", "Novo", "Importado", "Garantia".
- **Limpeza de caracteres**: Emojis, símbolos extras (ex: `★`, `🔥`, `🚀`, `!!`), espaços em branco duplicados e termos de caixa alta desnecessários são normalizados.

### 2. Comparação Fuzzy (FuzzySharp)
A similaridade entre o produto semente e os candidatos encontrados é calculada através da distância de Levenshtein:
- **Threshold de Confiança**: Deve ser maior ou igual a **85%** (`ConfidenceScore >= 85`).
- **Validação Anti-Colisão de Modelos**: O motor possui uma verificação rigorosa contra colisões de modelos distintos com nomes graficamente parecidos (ex: *Samsung Galaxy A20* vs *Samsung Galaxy S20*). Caso os modelos diferenciem por letras específicas de séries de produtos, a correspondência é rejeitada independente do score fuzzy.
- **Normalizador de Contingência (IAiNormalizer)**: Se o título for excessivamente complexo e o normalizador de texto local falhar, o sistema aciona uma chamada fallback para um serviço de IA (Gemini, Claude, LLM local) para extrair o modelo exato e marca do produto.

### 3. Filtro de Preço Máximo (Watchlist)
Cada entrada configurada pelo usuário na watchlist pode ter um preço teto (`MaxPrice`):
- O bot buscará ofertas nas plataformas parceiras.
- Se o menor preço encontrado para o produto semente for **superior** ao `MaxPrice` configurado, a oferta será ignorada para o ciclo de alertas e geração de propaganda.

## Critérios de Geração de Mídia e Preço

Para selecionar e diagramar os melhores criativos promocionais:
- **Preço Médio**: Calcula-se o preço médio aritmético entre as plataformas integradas para aquele produto.
- **Destaque de Menor Preço**: A plataforma que possuir o valor mais baixo do grupo é destacada com o marcador especial de melhor oferta (**★**).
- **Desconto Real**: Um desconto real é verificado quando o preço de venda atual é inferior ao preço de catálogo/original informado pela loja (`OriginalPrice > Price`).
- **Percentual de Economia**: A economia exibida é calculada em relação ao maior preço do grupo de comparação ou em relação ao preço antigo original da própria plataforma.

## Controle de Concorrência e Execução de Tarefas

- **Intervalo de Execução**: Por padrão, o bot executa a verificação a cada 15 minutos.
- **Sinalização Reativa**: O acionamento manual ("Executar Agora") libera o semáforo de background do worker instantaneamente, interrompendo o temporizador de repouso sem gerar race conditions no arquivo de watchlist ou no banco de dados.

## Gestão de Erros e Falhas Críticas

- **Fallback de WAF/Cloudflare**: Caso as requisições aos portais da Amazon ou Magazine Luiza sofram bloqueios temporários de rede ou WAF (Web Application Firewall), o bot ativa o modo simulação inteligente para manter o sistema operacional com dados sintéticos legítimos para testes.
- **Tratamento de Exceções em Lote**: Se um provedor falhar ou expirar a requisição, o motor Scatter-Gather captura o erro isoladamente e prossegue com a busca nas demais plataformas sem interromper o ciclo inteiro.

## Limites e Quotas (Geração/Processamento)

## Segurança e Acesso aos Dados do Usuário

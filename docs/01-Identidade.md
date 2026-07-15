# Identidade Visual e Experiência do Usuário (UI/UX)

Este documento estabelece as diretrizes de design, paleta de cores, tipografia, componentes e padrões de layout para a plataforma **RZND Media**. Toda a interface deve ser projetada para transmitir a sensação de uma ferramenta de desenvolvimento ou IDE profissional (como o Visual Studio Code), simplificada para máxima usabilidade.

---

## Princípios Gerais de Design

A interface do RZND Media segue quatro diretrizes fundamentais:
1.  **Estética de Ferramenta Técnica (IDE-like):** O design prioriza a produtividade, a organização e a clareza sobre elementos puramente decorativos. A disposição dos módulos é modular e simétrica.
2.  **Ausência de Elementos Supérfluos:** É expressamente **proibido o uso de emojis** em qualquer parte da interface do usuário. Todas as ações e indicações visuais devem ser mediadas por tipografia clara e ícones técnicos profissionais.
3.  **Ícones Profissionais em SVG:** Toda a iconografia deve utilizar gráficos vetoriais nativos (SVG) provenientes de bibliotecas consolidadas como *Lucide Icons*, *Material Icons* ou *Fluent Icons*.
4.  **Priorização de Estrutura sobre Efeitos:** A separação visual de painéis deve ser feita através de bordas claras e contraste de fundo. Gradientes, cores extremamente vibrantes, efeitos tridimensionais ou sombras pesadas devem ser evitados.

---

## Paleta de Cores e Temas

A paleta de cores é composta essencialmente por **Branco, Azul, Preto e Cinza**. Cores fora desse espectro são reservadas unicamente para sinalização funcional.

### Tema Escuro (Dark Theme - Base VS Code)
*   **Fundo Principal (Background):** `#18181C` (Cinza escuro neutro)
*   **Fundo de Painéis (Surface):** `#1E1E24` (Cinza intermediário)
*   **Fundo da Barra Lateral (Activity Bar):** `#111114` (Preto neutro)
*   **Texto Principal:** `#F3F4F6` (Branco acinzentado)
*   **Texto Secundário / Desabilitado:** `#9CA3AF` (Cinza médio)
*   **Bordas:** `#2D2D34` (Cinza para linhas divisórias de 1px)
*   **Destaques e Elementos Selecionados:** `#2563EB` (Azul corporativo)

### Tema Claro (Light Theme)
*   **Fundo Principal (Background):** `#F3F4F6` (Cinza claro limpo)
*   **Fundo de Painéis (Surface):** `#FFFFFF` (Branco puro)
*   **Fundo da Barra Lateral (Activity Bar):** `#E5E7EB` (Cinza médio claro)
*   **Texto Principal:** `#1F2937` (Preto acinzentado)
*   **Texto Secundário / Desabilitado:** `#6B7280` (Cinza escuro)
*   **Bordas:** `#D1D5DB` (Cinza claro)
*   **Destaques e Elementos Selecionados:** `#1D4ED8` (Azul escuro)

### Cores Funcionais (Semânticas)
Essas cores devem ser aplicadas estritamente em contextos informativos e nunca de forma decorativa:
*   **Verde (`#10B981`):** Sucesso, tarefa concluída, status disponível ou ativo.
*   **Vermelho (`#EF4444`):** Erro, falha crítica, bloqueio de processo ou ação de exclusão.
*   **Amarelo (`#F59E0B`):** Alerta, atenção especial, pendências de configuração ou sincronização em andamento.

---

## Componentes

### 1. Painéis e Cards
*   Os contêineres devem se parecer com os painéis e abas de uma ferramenta de desenvolvimento.
*   **Bordas retas:** Cantos arredondados mínimos (raio máximo de `2px` a `4px` apenas se necessário).
*   Evitar efeitos de sombra (*box-shadow*). A distinção do elemento contra o fundo é feita por bordas de `1px` com cores contrastantes (ex: `#2D2D34` no tema escuro).

### 2. Botões e Controles de Ação
*   Compactos, discretos e funcionais.
*   Sempre acompanhar ou ser representado por um ícone profissional (SVG).
*   O uso de fundos preenchidos com cores fortes é limitado ao botão de ação principal do formulário ou para estados específicos (ex: salvar ou executar lote). Ações secundárias devem usar estilo outline ou texto puro com ícone.

### 3. Formulários e Inputs
*   Campos de entrada com bordas de `1px` quadradas.
*   Indicadores de foco discretos usando a cor azul de destaque para a bordas, sem brilho ou reflexos excessivos.

---

## Padrão de Layout e Organização (IDE Layout)

A interface se organiza em uma grade modular e previsível dividida em três regiões principais:

1.  **Barra Lateral de Atividades (Sidebar/Activity Bar):**
    *   Posicionada no lado esquerdo, com largura fixa e contendo botões de navegação por ícones (Workspace, Modelos, Configurações).
2.  **Painel de Contexto (Context Explorer):**
    *   Um painel retrátil ao lado da barra de atividades que mostra a árvore de templates, a lista de providers de dados configurados ou opções do módulo ativo.
3.  **Área de Trabalho Principal (Main Workspace):**
    *   A maior área da tela, onde ocorre a configuração dos parâmetros de geração de mídia, a visualização do editor de templates e a exibição de resultados gerados.

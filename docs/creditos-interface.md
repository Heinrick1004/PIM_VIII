# Créditos da interface TaskFlow

## Pesquisa e atribuição

Em 12/11/2026 foram consultados:

- [Uiverse.io](https://uiverse.io/)
- [Uiverse.io — buttons](https://uiverse.io/buttons)
- [Uiverse.io — elements](https://uiverse.io/elements)

As três URLs carregaram uma página de apresentação do Uiverse/Design ("Teach your AI agent how to design" / "Get polished palettes, typography, components, and usage rules"), sem catálogo de elementos, página individual de componente, autoria ou licença de componente identificáveis. Portanto, **nenhum snippet ou componente individual do Uiverse foi copiado ou incorporado**; não há autoria nem licença específica que possa ser atribuída sem inventar dados. As URLs acima documentam a pesquisa, não uma atribuição de código.

As interações abaixo foram implementadas localmente com CSS/JavaScript nativos e HTML Razor do TaskFlow, usando padrões comuns de microinteração como referência visual:

1. Botões com transição, elevação curta e brilho azul discreto no hover.
2. Campos com transição de borda e anel de foco visível.
3. Cards do painel com entrada única e elevação sutil no hover.
4. Loading real em login, filtros e formulários/status durante o envio.
5. Menu lateral móvel com transição, overlay, suporte a Escape e foco por teclado.
6. Feedback de sucesso/erro/informação com entrada breve, sem animação repetitiva.

As animações são reduzidas por `prefers-reduced-motion: reduce`. Não há biblioteca externa de UI ou animação no runtime.

## Autoria

- Implementação HTML/CSS/JavaScript e adaptação ao design system: projeto TaskFlow / equipe Alunos Tech.
- Componentes Uiverse reutilizados: nenhum.
- Licenças de componentes de terceiros aplicáveis ao código da interface: nenhuma identificada, pois nenhum componente externo foi incorporado.

Caso o catálogo volte a disponibilizar componentes individuais, registrar para cada componente sua URL canônica, autor e licença declarada antes de reutilizar qualquer trecho.

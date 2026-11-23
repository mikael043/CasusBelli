# CasusBelli — Plano de desenvolvimento

**Status:** planejamento conceitual, sem autorização para implementação imediata.  
**Objetivo:** aprofundar a política feudal, a diplomacia e a guerra de Bannerlord sem transformar o mod em uma coleção ilimitada de sistemas.

## 1. Etapa atual — campanha de avaliação

Antes de alterar o código, jogar entre um e dois anos de campanha. Avaliar o funcionamento natural da reforma política: sucessões, hierarquia de títulos, conselhos, votações, facções, guerras civis, reintegração de rebeldes, diplomacia, tributos e convocação militar.

- [ ] Verificar estabilidade em sessões longas e após salvar/carregar.
- [ ] Observar se IA e jogador obedecem às mesmas regras.
- [ ] Avaliar frequência de guerras, reivindicações, coalizões e convocações.
- [ ] Conferir equilíbrio econômico, sucessões, pariato e participação eleitoral.
- [ ] Registrar situações concretas, reino, data, resultado esperado e observado.
- [ ] Corrigir defeitos comprovados antes de ampliar o escopo.

## 2. Serviço militar feudal

A missão atual de convocação termina quando o jogador se apresenta ao exército. A proposta é expandi-la para incluir a obrigação de servir durante um período definido no contrato feudal, sem retirar completamente a autonomia do vassalo.

| Lei de serviço | Prazo inicial sugerido |
|---|---:|
| Leve | 10 dias |
| Regular | 20 dias |
| Elevado | 30 dias |
| Máximo | 40 dias |

Os prazos são hipóteses de balanceamento, não valores definitivos.

- Preservar as competências atuais: soberano e marechal convocam o reino; duques convocam seus subordinados jurisdicionais.
- Dividir a missão em apresentação, serviço ativo e dispensa.
- Permitir solicitar dispensa por ferimentos, ameaça ao próprio feudo ou outras razões legítimas; considerar substituição por contribuição financeira.
- Abandono injustificado antes do prazo: perda de relação, lealdade e aumento de controvérsia, de acordo com a gravidade.
- Cumprimento: reconhecimento político proporcional; evitar exploração por convocações repetidas.
- Paz, dissolução do exército ou invalidação da convocação encerram a obrigação; não punir eventos fora do controle do vassalo.
- Aplicar obrigações equivalentes à IA, preservando sua capacidade de responder a emergências.

**Critério de conclusão:** missão persistente em save, cumprimento e deserção distinguíveis, dispensas válidas, regras equivalentes para IA e jogador.

## 3. Expansão dos Casus Belli

Toda modalidade deve definir elegibilidade, justificativa, alvo, autoridade para declarar, apoio político, objetivo militar, cálculo de pontuação e termos de paz. A IA deve escolher guerras conforme interesses e capacidade real, e não apenas disponibilidade de um motivo.

| Casus Belli | Elegibilidade proposta | Finalidade |
|---|---|---|
| Reivindicação territorial | Todos | Recuperar título específico de jure; preservar sistema existente. |
| Provocação fronteiriça | Todos | Incidente na fronteira, inspirado no Warband; reparação, retirada ou guerra limitada. |
| Libertação cultural | Todos | Libertar feudos de cultura própria sob domínio estrangeiro; definir previamente destinatário e legitimidade. |
| Invasão Vlandiana | Vlandia | Campanha de conquista regional, com objetivos delimitados e apoio extraordinário da nobreza. |
| Grande Saque | Nórdicos, Esturgia e Khuzait | Obter saques, tributos ou indenizações, sem exigir anexação territorial. |
| Reconquista Imperial | Reinos imperiais | Recuperar territórios de origem imperial sob domínio não imperial. |
| Reunificação imperial | Reinos imperiais | Submeter uma facção imperial rival, mediante condições especiais. |
| Guerra comercial | Todos | Obter compensações e concessões econômicas. |
| Guerra de sucessão | Reinos com pretendente legítimo | Instalar ou defender um pretendente ao trono. |
| Guerra punitiva | Todos | Responder a ofensa diplomática grave ou violação comprovada de acordo. |

### Provocações de fronteira

- Incidentes entre reinos vizinhos, relacionados a patrulhas, incursões, disputas locais ou ataques a propriedades.
- Possibilidade de investigação, pedido de desculpas, compensação, mobilização ou declaração formal de guerra.
- Evitar incidentes infinitos, motivos fabricados sem custo e guerras inevitáveis.
- Integrar chanceler, relações e autoridade; o soberano continua responsável pela reação.

### Regras gerais

- Controlar frequência, duração das justificativas, tréguas e sobreposição de objetivos.
- Tratar a paz como resolução do objetivo declarado; não converter todo conflito em conquista irrestrita.
- Grandes invasões e reunificações devem exigir condições mais rigorosas e ter consequências maiores.
- Definir o que constitui uma região e como identificar cultura/origem imperial no mapa antes da implementação.

**Critério de conclusão:** cada modalidade tem ciclo completo de declaração, guerra e paz, UI explicativa, comportamento de IA e testes de vitória, derrota e paz branca.

## 4. Apoio à guerra

Separar a pontuação militar (quem está vencendo) da disposição política de continuar lutando.

- Apoio individual de cada clã; visão agregada do reino e razões visíveis no painel de guerra.
- Considerar legitimidade do motivo, duração, perdas, despesas, ameaças aos feudos, lealdade, objetivos alcançados e interesses dos clãs.
- Apoio baixo pode levar a pedidos de paz, recusa de mobilização ou pressão política; apoio elevado facilita mobilização e continuidade.
- Permitir que um reino vitorioso militarmente busque a paz por exaustão política ou financeira.
- Integrar assembleia e autoridade da Coroa, preservando a política distinta de cada reino.
- Evitar que o peso da votação e a força militar sejam confundidos; avaliar quem tem direito a votar e quem apenas expressa opinião.

**Critério de conclusão:** o apoio tem efeitos observáveis nas decisões da IA, responde às mudanças na guerra e não substitui a pontuação militar existente.

## 5. Contratos e economia militar

Substituir a ideia de comprar soldados definitivamente por relações de serviço remunerado. Jogador e IA devem administrar soldos e vínculos contratuais.

### Ciclo do contrato

- Recrutamento mediante pagamento inicial e contratação por um ano de campanha.
- Soldos periódicos durante o serviço; custos proporcionais à categoria da tropa.
- Renovação voluntária, com condições transparentes; possibilidade de desligamento ao término.
- Atrasos geram insatisfação e eventualmente deserção; evitar penalizar duas vezes o mesmo soldo se o jogo base já o cobra.
- Avaliar indenizações às famílias de soldados mortos em combate, com registros financeiros agregados.
- Distinguir morte, ferimento, captura, deserção, promoção, transferência e dispensa.

### Estrutura dos grupos

Em paz, incentivar comitivas móveis menores, aproximadamente 50–80 soldados, mantendo a maior parte das forças nas guarnições. Durante a guerra, mobilizar reservas e concentrar tropas nos exércitos.

- Guarnições com custo reduzido, mas não gratuito; respeitar capacidade e necessidades de defesa.
- Contratos acompanham as tropas quando transferidas entre grupo e guarnição; transferências não reiniciam prazos nem apagam débitos.
- IA deve contratar, renovar, dispensar, guarnecer e mobilizar segundo seu orçamento, ameaça e renda prevista.
- Preferir lotes agregados de contratação a um objeto por soldado, preservando desempenho e compatibilidade com saves.
- Não impor limite rígido de 80 soldados; esse tamanho deve ser consequência econômica, não restrição arbitrária.

### Riscos a estudar

- Compatibilidade com o modelo nativo de salários, recrutamento e transferências.
- Falência generalizada dos clãs, falta de tropas para guerras e guarnições vazias.
- Exploração por dispensar/recontratar ou transferir tropas; crescimento excessivo do estado salvo.
- Como calcular um ano de campanha, custos de renovação e indenizações sem desestabilizar a economia.

**Critério de conclusão:** ciclo contratual funcional para IA e jogador, contabilidade consistente, contratos persistentes após transferências e reload, economia sustentável em testes prolongados.

## 6. Juramentos, justiça e intrigas

- Juramento de fidelidade ao soberano e renovação após sucessão.
- Possibilidade de recusar juramento, com consequências políticas e eventual formação de oposição.
- Acusações de traição, investigação pelo mestre dos espiões, julgamento, perdão e prisão política.
- Confisco ou revogação legítima condicionados a provas, autoridade e direitos feudais; abuso gera controvérsia.
- Conspirações e vazamento de informações conforme perícia, lealdade e interesses do mestre dos espiões.
- Integrar o sistema existente, sem criar uma segunda estrutura paralela de lealdade ou controvérsia.

## 7. Identidade política e eventos dos reinos

| Reino | Exemplos de crises e eventos |
|---|---|
| Vlandia | Sucessão de Derthert, disputas entre duques, legitimidade da Coroa e sede da corte. |
| Esturgia | Resistência de príncipes territoriais à centralização. |
| Battania | Rivalidade de clãs e disputas de prestígio. |
| Império do Norte | Conspirações senatoriais e eleições contestadas. |
| Império do Sul | Crises dinásticas e direitos hereditários. |
| Império do Oeste | Disputas de comando e usurpações militares. |
| Aserai | Rivalidades aristocráticas e comerciais. |
| Khuzait | Rivalidades entre chefes tribais e disputas de liderança. |
| Nórdicos | Conflitos entre grandes casas, assembleias e expedições de saque. |

Preferir crises emergentes baseadas em condições reais. Uma crise após a morte de Derthert pode ser favorecida por pretendentes fortes, eleição apertada e baixa lealdade, sem obrigatoriamente destruir a estabilidade de toda campanha.

## 8. Política dinástica

- Casamentos diplomáticos e alianças matrimoniais.
- Reivindicações hereditárias e disputas entre herdeiros.
- Regências, quando aplicáveis, com poderes e duração definidos.
- Preservar distinção entre sucessão da Coroa e herança dos títulos pessoais.
- Reutilizar contratos, títulos, reivindicações e tratados existentes.

## 9. Ordem de desenvolvimento proposta

| Prioridade | Etapa | Dependência |
|---|---|---|
| 0 | Campanha de avaliação e correções | Versão atual |
| 1 | Serviço militar feudal | Convocação e contratos feudais existentes |
| 2 | Novos Casus Belli | Diplomacia, registro de guerras e paz |
| 3 | Apoio à guerra | Objetivos e legitimidade dos conflitos |
| 4 | Juramentos, justiça e intrigas | Lealdade, controvérsia, conselho e facções |
| 5 | Contratos e economia militar | Estudo da economia nativa e IA |
| 6 | Eventos culturais e política dinástica | Sistemas políticos estabilizados |

Ordem revisável após a campanha. Desenvolver e testar um módulo de cada vez. Não iniciar toda a lista simultaneamente.

## 10. Projeto separado — aventureiros autônomos (nome provisório: Homo Novus)

**Decisão de escopo:** esta ideia não faz parte do CasusBelli. Criar e manter um projeto independente em estado conceitual até existir disposição para desenvolvê-lo.

### Conceito

Ao surgir o personagem do jogador em uma cultura, outros cinco heróis controlados pela IA aparecem em condições iniciais equivalentes. Cada um possui identidade, ambições, habilidades e trajetória próprias. A corrida por riqueza, prestígio e ascensão social é emergente, não uma competição com progresso artificialmente igualado.

### Progressão proposta

- Recrutar, treinar, remunerar e manter grupos de tropas.
- Combater bandidos, participar de torneios, buscar renda e desenvolver habilidades.
- Acumular renome, formar clãs e progredir para mercenariado e vassalagem.
- Estabelecer rivalidades, alianças, famílias e dinastias.
- Conquistar posição política e feudos por caminhos legítimos disponíveis no jogo.
- Admitir fracasso, captura, morte ou mudança de ambição, sem privilégios artificiais para manter todos competitivos.

### Questões de viabilidade

- Como implementar progressão autônoma de heróis sem sobrecarregar o motor da campanha?
- A IA consegue manter grupos economicamente viáveis e evoluir sem bônus ocultos?
- Como evitar colisões de clãs, identificadores e eventos de nascimento/carregamento?
- Como criar objetivos coerentes sem microgerenciar cada ação dos personagens?
- Começar com um protótipo de um aventureiro e aumentar para cinco somente após validação.

### Compatibilidade

Os dois mods devem funcionar independentemente. Quando instalados juntos, um aventureiro que se torne vassalo pode entrar naturalmente nos sistemas do CasusBelli. Integração opcional por interfaces estáveis, sem dependência circular nem duplicação de economia, política ou hierarquia.

## 11. Limites arquiteturais e definição de pronto

- CasusBelli permanece centrado em política, diplomacia, feudalismo e guerra.
- Evitar classes duplicadas, dependências circulares e comportamentos que disputem o controle dos mesmos eventos nativos.
- Reutilizar modelos e serviços existentes; separar estado persistente, regras, IA e interface quando fizer sentido.
- Registrar parâmetros ajustáveis e justificar mudanças de balanceamento.
- Para cada expansão, testar jogador, IA, casos extremos, compatibilidade de saves, desempenho e interação com os sistemas existentes.
- Considerar uma versão completa somente quando o ciclo de jogo estiver funcional, estável e compreensível; ideias futuras não são requisitos para concluir a versão atual.
- Antes da versão final, remover rótulos, pontuações e comentários de desenvolvimento da interface (legitimidade e previsão de apoio); preservar apenas informações úteis ao jogador.

### Política ducal — ciclo de validação

1. Usurpação exige controle de pelo menos 51% dos condados diretos, inclusive com reivindicação madura; dentro do reino, também exige reivindicação madura.
2. Diagnóstico de legitimidade: direito, prestígio, base fundiária e controle ducal. Valores provisórios, sem efeitos automáticos.
3. Prévia de apoio: paridade plena, nível 3 e base fundiária (ou ex-titular), reivindicação madura e restrições da Coroa; pares avaliam legitimidade, risco militar, controvérsia, guerra, patrimônio e relações limitadas. A prévia não cria alianças nem guerras.
4. Disputa ducal aberta explicitamente após reivindicação madura. Emissários aos pares plenos; maioria absoluta dos pares elegíveis, revalidação do apoio e coalizão militar ao menos equivalente à defesa antes do ultimato. Reutiliza guerra pessoal e paz; transfere apenas o ducado, mantém os feudos subordinados, e dá ao antigo titular reivindicação de ex-titular. Iniciativa autônoma ainda desativada.
5. IA ducal conservadora: avaliação semanal (15% por reino estável, no máximo uma iniciativa global por avaliação); exige condado direto ou direito de ex-titular, paridade plena e nível 3, paz externa, ausência de crise civil e autoridade inferior a 65. Nunca presume apoio do jogador. Fabricação paga e reserva de 5.000 denários e influência; usurpação territorial usa o modelo existente, ou disputa política exige maioria prevista e superioridade militar de 1,5:1, além das regras comuns. Apenas um projeto ducal por reino; pausa em guerra/crise, prazo de 84 dias para recrutamento antes de desistir sem perder o direito, cooldown de 84 dias por reino e 168 dias por título e clãs após resolução. Testar em campanha prolongada antes de flexibilizar.
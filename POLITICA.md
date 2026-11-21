# Política, títulos e diplomacia

## Consultar a campanha

No menu do reino (K), abra **CasusBelli**. **Hierarquia** mostra o titular de direito, a posse efetiva, as leis pessoais e os atos disponíveis para o título selecionado. **Pares do reino** lista o pariato. **Conselho** permite ao soberano nomear ou dispensar conselheiros. **Disputas internas** reúne emissários, ultimatos e acordos entre vassalos. **Política** abre as leis, a última votação nominal, os atributos dos líderes, as contas, as coalizões e as embaixadas.

### Sucessão e direitos feudais

A campanha nova lê `Modules/CasusBelli/ModuleData/titles.xml` da instalação do jogo. O arquivo distribuído define nove reinos, 33 ducados, 57 condados e 76 baronias. A geração automática acrescenta fortificações sem definição e os senhorios das aldeias vinculadas. O carregador verifica os vínculos, as leis, os identificadores territoriais repetidos e as fortificações de cada reino presente. A seção nórdica pode coexistir com uma campanha sem esse reino.

O soberano eleito pelo Bannerlord recebe a Coroa. Os títulos pessoais do falecido seguem a herança e a lei de gênero de cada contrato. A lei de sucessão declarada em um ducado, condado ou baronia pode substituir a herdada do nível superior. A partilha distribui títulos sucessivamente entre os herdeiros elegíveis. Na falta de um herdeiro elegível, um título pessoal pode ficar vago.

Uma conquista ou cessão de paz muda a posse efetiva. O reconhecimento, a concessão, a revogação, a sucessão e a vitória em uma disputa pessoal podem transferir o direito legal. Uma fortificação sob outra Coroa acumula jurisdição à razão de 0,000793 por dia, cerca de 1.262 dias para completar a transferência partindo do zero. O progresso de Coroas concorrentes diminui. Saves com a hierarquia já inicializada conservam as concessões, reivindicações e contratos da campanha.

### Assembleia, governo e autoridade

Pares menores e plenos participam da assembleia. Clãs eliminados e mercenários ficam fora do eleitorado. Cada voto combina intensidade de apoio e peso do clã. A abstenção soma zero pontos. A ata identifica o nobre, o clã, o peso e a opção apoiada. O registro conserva a última decisão encerrada de cada reino.

| Reino | Autoridade inicial | Regra política |
| --- | ---: | --- |
| Vlândia | 50 | Os títulos territoriais dão peso ao voto e favorecem candidatos à Coroa. |
| Estúrgia | 40 | Os príncipes territoriais têm peso eleitoral e a Coroa começa abaixo do limiar de revogação. |
| Battânia | 35 | Cada clã tem o mesmo peso por intensidade de apoio. Prestígio e relações favorecem os candidatos. |
| Império do Norte | 40 | O pariato pesa na assembleia e a influência favorece os candidatos. |
| Império do Sul | 70 | A dinastia reinante recebe preferência na escolha do soberano. |
| Império do Oeste | 65 | Liderança favorece os candidatos e o número de grupos militares dá peso aos votos. |
| Aserai | 60 | O patrimônio do líder afeta seu peso eleitoral e suas chances de sucessão. |
| Khuzait | 55 | Grupos militares dão peso ao clã. Tática e relações favorecem os candidatos. |
| Nórdicos | 35 | A assembleia dá o mesmo peso a cada clã e valoriza prestígio e vínculos entre nobres. |

Com **45** de autoridade, o suserano imediato pode revogar títulos. Revogar sem direito prévio custa 4 de autoridade e acrescenta 10 de controvérsia. Com **60**, o soberano pode contrariar a maioria em uma decisão ordinária, ao custo de 5 de autoridade e 8 de controvérsia. Também pode gastar 30 de influência para impor 60 dias de paz entre dois vassalos em disputa. Com **75**, a Coroa proíbe novos ultimatos privados. A eleição ao trono é vinculante em todos os níveis de autoridade.

Conquistas, derrotas territoriais, sucessões, decisões e infrações alteram a autoridade. Lealdade pertence ao vínculo do nobre com o soberano atual. Controvérsia acompanha o nobre mesmo depois de uma mudança de reino. Ambas entram na política de nomeações e no cálculo do apoio. A sede da corte considera o domínio da nova casa reinante e os seus centros urbanos. Pravend é uma possibilidade quando essas condições a favorecem.

### Conselho e finanças

O marechal e o soberano convocam grupos do reino. Um duque pode reunir os grupos dos nobres dos condados da sua jurisdição. A autoridade e a lei de serviço militar alteram o custo da convocação para jogador e IA. O chamado ao jogador continua como missão.

O chanceler acelera a fabricação de reivindicações da Coroa conforme o seu Charme, pode mediar tréguas internas e conduz as embaixadas. Para pedir reconhecimento, o chefe do clã que detém o feudo precisa ter uma reivindicação madura e estar em paz com o titular legal. O reconhecimento depende de parecer favorável ou da decisão do jogador quando este representa uma das autoridades envolvidas.

O intendente recolhe tributos semanalmente. O ponto de partida é 25 denários por fortificação, ajustado pela lei fiscal do feudo, pela competência e pela autoridade. O pagamento sai do ouro disponível do líder vassalo e entra no orçamento nativo do reino. A conta registra receitas, despesas do domínio real, financiamento de exércitos e valores que ficaram por arrecadar naquela semana. A inadimplência afeta a influência e a posição política do devedor.

O mestre dos espiões procura reivindicações em preparação e coalizões. Perícia, lealdade, amizade com o suspeito e participação na coalizão alteram a divulgação. O soberano também pode mandar investigar um nobre por 15 de influência. Uma investigação ocupa a rede por 21 dias. A exposição de uma coalizão dificulta seu recrutamento.

### Facções, disputas e diplomacia

Uma coalizão pode pressionar pela sucessão, autonomia, independência, redução de tributos ou fortalecimento da Coroa. O emissário leva três dias para chegar e o líder pode despachar uma missão por semana. Clãs podem aderir, recusar ou retirar apoio. Para apresentar o ultimato, o líder precisa ter um feudo, reunir pelo menos dois clãs e representar 40% da força de campanha do reino. A coalizão pode se dissolver por falta de apoio, morte, mudança de reino ou expiração. Uma derrota eleitoral apertada pode dar origem a uma candidatura de oposição.

A aceitação do ultimato aplica a concessão correspondente. Uma coalizão separatista ou hostil que entra em guerra forma um reino no mapa com os clãs comprometidos. O conflito usa o registro de guerras, as tréguas e o WarScore existentes. A resolução pode reunificar os clãs, mudar a casa reinante, conceder autonomia ou tributos menores, ou manter um reino independente. Os adversários numa disputa pessoal por um condado ou baronia continuam pertencendo ao mesmo reino. Os combates privados são simulados com as tropas de campanha dos apoiadores, preservando as relações militares do reino no mapa.

Uma guerra por um feudo cujo titular legal já pertence ao atacante é uma reconquista. Os demais objetivos territoriais com direito válido seguem a via da reivindicação. As embaixadas custam 100 denários do tesouro e podem negociar um pacto anual ou uma aliança nativa. O acordo defensivo pode trazer o aliado para a guerra. Romper um pacto custa 8 de autoridade, 18 de controvérsia e 15 de relação entre soberanos. Resgates, indenizações e armistícios usam a negociação de paz existente.

### Diagnósticos

- `casus_belli.politica vlandia` exibe o estado político, o tesouro, a coalizão e a votação registrada. Sem argumento, consulta o reino do jogador.
- `casus_belli.auditar_politica` procura problemas de hierarquia, titulares falecidos e guerras simultâneas duplicadas para o mesmo par.
- `Tests/Run.ps1` compila o projeto e executa as regressões contra as DLLs da instalação local. Exercita os dois formatos de XML, a sucessão, a jurisdição, a votação nominal, os pesos reais, o veto, os vínculos Harmony, a serialização e a navegação da hierarquia.

## Validação

O ambiente de teste usa Bannerlord 1.4.8.119303. A suíte compara o catálogo com os dados instalados e carrega os 133 feudos explícitos do XML completo e os 17 do formato anterior. A campanha nova inicializou 431 títulos, incluindo os senhorios gerados, com oito reinos presentes. A seção nórdica passou pelo teste de hierarquia com um reino simulado. Esta instalação não inclui uma campanha nórdica jogável.

O save anterior `save009` abriu com os novos comportamentos políticos e avançou por 90 dias em uma execução de integração. As gravações desse ensaio foram redirecionadas à memória. Uma execução distinta com criação automatizada de personagem chegou ao mapa e encontrou uma exceção em `MBMusicManager.GetCampaignMusicTheme`. O diagnóstico do processo identificou a rotina de música do mapa na pilha. O teste isolado da hierarquia também verifica o menu e suas ações com os widgets Gauntlet reais.

No cenário controlado de sucessão, o save abriu no dia 91106 com 431 títulos e duas guerras ativas. A morte de Derthert abriu uma decisão do reino. Em paralelo, o teste preparou o apoio dos clãs do Império do Sul e apresentou o ultimato pela rotina do mod. A liga surgiu no mapa, o número de reinos passou de oito a nove e o registro recebeu uma guerra de sucessão. A campanha foi salva com os dois reinos em guerra. A rendição do Império reunificou os clãs e colocou a casa do pretendente no trono. Esse cenário verifica o desfecho político de apoios e rendição deliberadamente preparados.

Esse ensaio terminou no dia 91196, após 90 dias. A leitura pelo jogo da gravação feita durante a rebelião retornou sucesso. A auditoria final contou 434 títulos e oito guerras ativas, com dez reinos vivos. Não encontrou vínculos feudais inválidos, titulares falecidos ou registros de guerra ativos duplicados para o mesmo par. A janela de paz também informa o destino político da coalizão junto das eventuais indenizações.

Os valores de 40% de força para o ultimato, 60 de autoridade para contrariar a assembleia e os custos das missões são escolhas de equilíbrio. As simulações verificam efeitos e persistência. A frequência desejável de rebeliões em campanhas de várias gerações ainda depende de observação em jogo.

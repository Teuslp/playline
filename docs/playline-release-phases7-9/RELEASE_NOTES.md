# Playline 0.9.2-beta

Esta atualização consolida o novo visual compacto e os controles de posicionamento do Playline para Windows x64.

## Destaques

- Instalador por usuário, sem necessidade de privilégios administrativos.
- Pacote portátil ZIP com o mesmo binário self-contained do instalador.
- Runtime .NET incluído: não é preciso instalar o .NET separadamente.
- Descoberta sob demanda de jogos Steam e Epic Games.
- Capas dos launchers priorizadas e ícones de executáveis extraídos em alta resolução pelo Shell do Windows.
- Renovação automática do cache antigo de ícones em bibliotecas já existentes.
- Adição manual por executável ou atalho, favoritos, edição e reordenação.
- Barra minimalista com largura adaptativa e 68 px no tamanho médio.
- Vidro neutro quase transparente, sem tint azul/roxo e com cantos suavizados.
- Opção persistente de barra horizontal ou vertical.
- Movimento direto pelos três pontos, bloqueio de posição e centralização horizontal no monitor atual.
- Seletores nativos do Windows para procurar executável, pasta de trabalho e imagem; não é necessário colar caminhos.
- Configurações de aparência, posição, comportamento pós-abertura e início com o Windows.
- Recuperação automática de arquivos JSON corrompidos, preservando uma cópia `.bak` para diagnóstico.
- Logs críticos com rotação e retenção limitadas.
- Deduplicação indexada para bibliotecas grandes.

## Instalação

Execute `Playline-Setup-x64.exe`. A instalação padrão fica em `%LOCALAPPDATA%\Programs\Playline` e cria um atalho no menu Iniciar. O atalho na área de trabalho é opcional.

Para uso portátil, extraia todo o conteúdo de `Playline-Portable-x64.zip` antes de executar `Playline.exe`. Os dados pessoais continuam em `%LOCALAPPDATA%\Playline` nos dois formatos.

## Atualização e remoção

Executar uma versão mais nova do instalador atualiza a instalação existente. A desinstalação remove os arquivos do programa e seus atalhos, mas preserva biblioteca, configurações, cache e logs do usuário.

## Integridade

Compare os arquivos baixados com os hashes SHA-256 em `checksums.txt`.

## Limitações conhecidas

- Binários e instalador ainda não possuem assinatura de código; o Windows pode exibir um aviso de reputação.
- O ícone atual é temporário e será substituído antes da versão 1.0.
- Esta distribuição é exclusiva para Windows x64.
- A validação física de DPI foi realizada no equipamento disponível; o aplicativo declara e usa Per-Monitor V2, mas combinações adicionais de monitores ainda merecem testes de campo.
- Steam e Epic são as únicas fontes automáticas desta versão; não há sincronização em nuvem, contas ou scan contínuo.

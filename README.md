# Playline

O Playline é uma barra minimalista para organizar e iniciar jogos no Windows. A versão atual é a `0.9.2-beta`.

## Interface

- Barra compacta, sem moldura, com tamanho adaptativo e orientação horizontal ou vertical.
- Alturas pequena, média e grande de 58, 68 e 80 px.
- Imagens dos jogos na barra e na lista de detecção.
- Capas dos launchers priorizadas e ícones de executáveis extraídos em alta resolução pelo Shell do Windows.
- Modos somente imagem e imagem + nome.
- Vidro neutro quase transparente, com contorno arredondado suave e sem tint azul/roxo.
- Capas em alta qualidade, segmentos discretos e menu com três pontos circulares.
- Modo vertical com a mesma escala legível de capas do layout horizontal.
- Dropdowns escuros e legíveis, com seleção e foco consistentes com o restante da interface.
- Sem dependência visual externa, animação contínua ou processo auxiliar.

## Recursos

- Adição manual por executável `.exe` ou atalho `.lnk` usando o seletor nativo do Windows.
- Seletores nativos para executável, diretório de trabalho e imagem durante a edição.
- Detecção sob demanda de bibliotecas Steam e manifests da Epic Games.
- Favoritos, edição, remoção e reordenação por arrastar ou pelo menu.
- Movimento direto pelos três pontos, posição fixa e centralização horizontal por monitor.
- Configurações de tamanho, modo de exibição, orientação, posição, `AlwaysOnTop`, auto-hide e ação pós-abertura.
- Inicialização opcional com o Windows por usuário e ícone na área de notificação.
- Navegação por teclado, nomes acessíveis, tooltips e feedback não bloqueante.
- Persistência JSON com gravação temporária, recuperação de corrupção e backup `.bak`.
- Logs críticos com rotação e retenção limitadas.
- Nenhum scan contínuo, watcher, polling ou processo auxiliar permanente.

## Instalação

Os arquivos prontos estão em `artifacts/`:

- `Playline-Setup-x64.exe`: instalação por usuário, sem elevação administrativa.
- `Playline-Portable-x64.zip`: os mesmos binários em formato portátil.
- `checksums.txt`: hashes SHA-256 dos dois pacotes.
- `release-notes.md`: notas da versão.

O instalador usa `%LOCALAPPDATA%\Programs\Playline`. O runtime .NET está incluído; não é necessário instalá-lo separadamente. A desinstalação remove programa e atalhos, mas preserva a biblioteca e as configurações.

No pacote portátil, extraia todo o ZIP antes de abrir `Playline.exe`.

## Dados locais

Os dois formatos usam `%LOCALAPPDATA%\Playline`:

```text
Playline/
├── games.json
├── settings.json
├── cache/
│   └── icons/
└── logs/
```

## Desempenho verificado

Medições em Release self-contained x64 no ambiente de validação:

| Cenário | Resultado |
|---|---:|
| Carga de 250 jogos, antes da indexação | 61,2 ms |
| Carga de 250 jogos, depois da indexação | 5,4 ms |
| Janela pronta, 10 jogos | 1,17 s em média |
| Janela pronta, 250 jogos | 2,39 s em média |
| CPU ociosa após estabilização, amostra de 10 s | 0 ms |
| Escrita ociosa, amostra de 10 s | 0 bytes |
| Processos filhos ociosos | 0 |
| Publicação descompactada | 155,67 MB |

Os 250 botões também foram encontrados na árvore de acessibilidade. Virtualização não foi adicionada porque o ganho não justificou o risco para drag-and-drop e navegação por teclado nesta escala.

## Desenvolvimento

Pré-requisitos: Windows x64 e .NET 10 SDK.

```powershell
dotnet restore Playline.sln
dotnet build Playline.sln -c Release
dotnet test Playline.sln -c Release
dotnet run --project src/Playline.App/Playline.App.csproj
```

Para medir bibliotecas simuladas e consumo ocioso:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Measure-Release.ps1
```

Para gerar instalador, ZIP e checksums, instale o Inno Setup 6 e execute:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Build-Release.ps1
```

Para validar os pacotes em diretórios temporários isolados:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Validate-Release.ps1
```

## Estrutura

```text
src/
├── Playline.App/        # barra e janelas WPF
├── Playline.Core/       # modelos e regras
├── Playline.Discovery/  # Steam, Epic e arte local
├── Playline.Storage/    # JSON, caminhos e logs
└── Playline.Windows/    # atalhos, imagens, execução e startup
tests/Playline.Tests/    # testes unitários e de integração
tools/Playline.Diagnostics/
installer/Playline.iss
scripts/
```

## Limitações conhecidas

- Distribuição exclusiva para Windows x64.
- Binários ainda não possuem assinatura de código; o Windows pode exibir aviso de reputação.
- O ícone do aplicativo é um placeholder temporário para a fase beta.
- A validação física de DPI foi feita no equipamento disponível; a aplicação usa Per-Monitor V2.
- Steam e Epic são as únicas fontes automáticas desta versão.
- Não há contas, nuvem, telemetria, plugins, gamepad ou scan contínuo.

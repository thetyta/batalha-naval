# ⚓ Batalha Naval P2P - Protocolo Customizado

Este projeto é um jogo de Batalha Naval via console desenvolvido em C# (.NET) para a disciplina de Redes de Computadores. O sistema utiliza uma arquitetura Peer-to-Peer (P2P) híbrida e implementa um protocolo de comunicação próprio, permitindo partidas em rede local ou através de redes virtuais privadas (VPN).

## 🛠️ Arquitetura e Protocolo

O projeto cumpre as exigências da disciplina dividindo a comunicação em duas fases:

- **Fase TCP (Transferência de Arquivo)**: Assim que a conexão é estabelecida, o Host envia automaticamente um arquivo (regras.txt) para o Cliente, garantindo a entrega confiável e cumprindo o requisito de transferência de arquivos em TCP.

- **Fase UDP (Troca de Mensagens)**: O loop principal de combate (envio de coordenadas de ataque e respostas de "Água/Fogo") acontece inteiramente via datagramas UDP, otimizando a velocidade.

Todos os pacotes trafegados possuem um **Cabeçalho Customizado de 36 bytes** perfeitamente alinhado (Identificador, Versão, Tipo de Mensagem, Num. Sequência, Timestamp, Token, Checksum, Tamanho Payload) para posterior análise de segurança via Wireshark.

## 📋 Requisitos

- **.NET 8.0** ou superior
- **Windows** (com suporte a Radmin VPN) ou **Linux/macOS** (com acesso à rede)
- **Radmin VPN** (ou qualquer rede compartilhada) para conexão remota
- Acesso à porta **8080** (pode ser configurada modificando a constante `PORTA` no código)

## 📁 Arquivos do Jogo

Para o jogo funcionar, é obrigatório que exista um arquivo chamado `mapa.txt` na mesma pasta do executável. Este arquivo define o posicionamento dos seus navios num grid 5x5, usando `0` para Água e `1` para Navio, separados por espaço.

### Exemplo de mapa.txt:
```
0 1 0 0 0
0 1 0 1 1
0 0 0 0 0
1 1 1 0 0
0 0 0 0 0
```

## ⚠️ REGRA DE OURO: Sincronia de Navios

Por ser um jogo P2P descentralizado, **ambos os jogadores devem possuir a EXATA mesma quantidade de navios** (números 1) nos seus respectivos arquivos `mapa.txt`.

- **Exemplo**: Se combinarem de jogar com 5 navios, o arquivo do Jogador 1 deve ter cinco números 1, e o do Jogador 2 também deve ter cinco números 1 (não importa a posição).

- ⚡ **Se houver divergência** (ex: um tem 1 navio e o outro tem 5), o jogo de quem tem menos navios vai declarar vitória e encerrar a conexão prematuramente, causando um erro ("Crash") no console do adversário.

## 🚀 Como Jogar (Conexão via Radmin VPN)

Certifiquem-se de que o **Windows Firewall** está configurado para permitir o aplicativo em Redes Públicas e Privadas.

### Passo 1: Preparação
1. Ambos os jogadores devem estar na mesma rede do **Radmin VPN**.
2. Cada jogador deve ter seu próprio arquivo `mapa.txt` com a **mesma quantidade de navios**.

### Passo 2: Jogador 1 (Host)
1. Abre o jogo e escolhe a opção **1 - Criar uma sala**.
2. O jogo ficará aguardando na porta **8080**.
3. O Jogador 1 deve passar o seu **IP do Radmin VPN** para o Jogador 2.

### Passo 3: Jogador 2 (Cliente)
1. Abre o jogo e escolhe a opção **2 - Entrar na sala**.
2. Digita o **IP do Radmin VPN** que o Jogador 1 enviou.

### Passo 4: O Combate
- O jogo pede coordenadas de ataque usando letras e números reais de Batalha Naval.
- **Exemplo de disparos válidos**: A1, B3, C5.
- Os jogadores se alternam entre ataques.
- **FOGO** = Acertou um navio
- **AGUA** = Errou
- O primeiro a destruir todos os navios do adversário vence!

## 📊 Estrutura do Código

### Principais Funções

| Função | Descrição |
|--------|-----------|
| `Main()` | Ponto de entrada; apresenta menu e inicia servidor ou cliente |
| `CarregarTabuleiro()` | Lê `mapa.txt` e popula a matriz de navios |
| `LoopDoJogo()` | Loop principal onde acontecem turnos, ataques e validações |
| `CriarPacoteProtocolo()` | Monta pacote com cabeçalho de 36 bytes conforme especificação |
| `LerPayloadProtocolo()` | Extrai dados úteis do pacote recebido |

### Fluxo de Dados

```
1. Conexão TCP
   ├─ Host aguarda na porta 8080
   ├─ Cliente se conecta
   └─ Host envia regras.txt via TCP

2. Jogo via UDP
   ├─ Host entra no LoopDoJogo() como primeiro jogador (meuTurno=true)
   ├─ Cliente entra no LoopDoJogo() como segundo jogador (meuTurno=false)
   ├─ Alternância de turnos com troca de pacotes UDP
   └─ Vitória quando alguém destrói todos os navios adversários
```

## 💻 Compilação (Opcional)

Se precisar gerar o executável (.exe) para envio, abra o terminal na pasta do projeto e rode o comando do SDK do .NET:

```bash
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

O arquivo gerado estará em `bin/Release/net8.0/win-x64/publish/`.

### Problema: "mapa.txt inválido ou inexistente"

**Solução:**
- O arquivo `mapa.txt` **deve estar na mesma pasta** do executável ou do arquivo Program.cs ao usar `dotnet run`
- Verifique a formatação: deve ser uma matriz 5x5 com 0s e 1s separados por espaço
- Cada linha deve ter exatamente 5 números

### Problema: Um jogador ganha instantaneamente (ambos com navios)

**Solução:**
- A quantidade de navios está **diferente** entre os dois mapas!
- Sincronize: conte os 1s em ambos os `mapa.txt` e deixe iguais
- Exemplo: se um tem 8 navios e outro tem 5, quem tem 5 vai "vencer" quando destruir os 5 do adversário

### Problema: Mensagens "Crash" ou desconexão inesperada

**Solução:**
- Pode ser perda de pacotes UDP na rede (normal em redes com latência alta)
- Tente reconectar
- Se persistir, verifique a qualidade da conexão Radmin VPN

## 🔐 Segurança

- O **Token** (`SESSAO_SECRETA_1`) é fixo e pode ser alterado no código para maior segurança
- Não há criptografia de dados; use apenas em redes confiáveis
- O protocolo foi desenvolvido para fins acadêmicos, não para produção


**⚓ Divirta-se na Batalha Naval! ⚓**


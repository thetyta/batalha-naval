using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;

class Program
{
    static int[,] tabuleiro = new int[5, 5];
    static int totalNavios = 0;
    const int PORTA = 8080;
    const string TOKEN = "SESSAO_SECRETA_1";
    const char CHAVE_XOR = 'K';

    static void Main()
    {
        CarregarTabuleiro();

        Console.WriteLine("⚓ Batalha Naval ⚓");
        Console.Write("Ativar criptografia nos pacotes? (S/N): ");
        bool usarCripto = Console.ReadLine().Trim().ToUpper() == "S";

        Console.WriteLine("\n1 - Criar uma sala");
        Console.WriteLine("2 - Entrar na sala");
        Console.Write("O que vamos fazer? ");
        string escolha = Console.ReadLine();

        if (escolha == "1")
        {
            Console.WriteLine("\nEsperando conexão");
            TcpListener listener = new TcpListener(IPAddress.Any, PORTA);
            listener.Start();
            using TcpClient tcpClient = listener.AcceptTcpClient();
            Console.WriteLine("Conectado!\n");

            if (!File.Exists("regras.txt")) File.WriteAllText("regras.txt", "Regras: Destrua todos os navios!");
            byte[] pacoteTCP = CriarPacoteProtocolo(1, TOKEN, File.ReadAllText("regras.txt"), usarCripto);
            tcpClient.GetStream().Write(pacoteTCP, 0, pacoteTCP.Length);
            
            IPEndPoint endpointAdversario = (IPEndPoint)tcpClient.Client.RemoteEndPoint;
            using UdpClient udpClient = new UdpClient(PORTA);
            LoopDoJogo(udpClient, endpointAdversario.Address.ToString(), true, usarCripto);
        }
        else
        {
            Console.Write("\nManda o IP do adversário: ");
            string ip = Console.ReadLine();
            
            using TcpClient tcpClient = new TcpClient(ip, PORTA);
            Console.WriteLine("Conectou!\n");

            byte[] bufferTCP = new byte[1024];
            int bytesLidos = tcpClient.GetStream().Read(bufferTCP, 0, bufferTCP.Length);
            if (bytesLidos > 0)
            {
                byte[] dadosRecebidos = new byte[bytesLidos];
                Array.Copy(bufferTCP, dadosRecebidos, bytesLidos);
                string arquivoRecebido = LerPayloadProtocolo(dadosRecebidos, usarCripto);
                File.WriteAllText("regras_recebidas.txt", arquivoRecebido);
                Console.WriteLine("Arquivo recebido via TCP.");
            }
            
            using UdpClient udpClient = new UdpClient(PORTA);
            LoopDoJogo(udpClient, ip, false, usarCripto);
        }
    }

    static void CarregarTabuleiro()
    {
        if (!File.Exists("mapa.txt"))
        {
            Console.WriteLine("'mapa.txt' invalido ou inexistente");
            Environment.Exit(1);
        }

        string[] linhas = File.ReadAllLines("mapa.txt");
        for (int i = 0; i < 5; i++)
        {
            string[] colunas = linhas[i].Split(' ');
            for (int j = 0; j < 5; j++)
            {
                tabuleiro[i, j] = int.Parse(colunas[j]);
                if (tabuleiro[i, j] == 1) totalNavios++;
            }
        }
        Console.WriteLine($"Mapa carregado. Total de navios na frota: {totalNavios}\n");
    }

    static void LoopDoJogo(UdpClient udpClient, string ipAdversario, bool meuTurno, bool usarCripto)
    {
        IPEndPoint alvo = new IPEndPoint(IPAddress.Parse(ipAdversario), PORTA);
        int meusPontos = 0;
        int pontosAdversario = 0;

        while (meusPontos < totalNavios && pontosAdversario < totalNavios)
        {
            Console.WriteLine($"\n[ PLACAR ]\n Você: {meusPontos} |X| Inimigo: {pontosAdversario}");

            if (meuTurno)
            {
                Console.Write("Sua vez! Onde vai atirar? (ex: A1, C3): ");
                string ataque = Console.ReadLine().Trim().ToUpper(); 

                byte[] pacote = CriarPacoteProtocolo(2, TOKEN, ataque, usarCripto);
                udpClient.Send(pacote, pacote.Length, alvo);

                string resposta = "";
                do 
                {
                    IPEndPoint remetente = new IPEndPoint(IPAddress.Any, 0);
                    byte[] respostaBytes = udpClient.Receive(ref remetente);
                    resposta = LerPayloadProtocolo(respostaBytes, usarCripto).Trim().ToUpper();
                } while (resposta != "FOGO" && resposta != "AGUA"); 

                if (resposta == "FOGO") 
                {
                    Console.WriteLine("Você acertou um navio!");
                    meusPontos++;
                } 
                else 
                {
                    Console.WriteLine("Tiro na água.");
                }

                meuTurno = false;
            }
            else
            {
                Console.WriteLine("aguardando o inimigo atirar...");
                
                string ataqueInimigo = "";
                IPEndPoint remetente = new IPEndPoint(IPAddress.Any, 0);
                do 
                {
                    byte[] pacoteRecebido = udpClient.Receive(ref remetente);
                    ataqueInimigo = LerPayloadProtocolo(pacoteRecebido, usarCripto).Trim().ToUpper();
                } while (ataqueInimigo == "FOGO" || ataqueInimigo == "AGUA");
                
                Console.WriteLine($"o inimigo atirou na posição: {ataqueInimigo}");

                int linha = ataqueInimigo[0] - 'A'; 
                int coluna = int.Parse(ataqueInimigo.Substring(1)) - 1;

                string status = "AGUA";
                if (linha >= 0 && linha < 5 && coluna >= 0 && coluna < 5)
                {
                    if (tabuleiro[linha, coluna] == 1)
                    {
                        status = "FOGO";
                        tabuleiro[linha, coluna] = 0;
                        pontosAdversario++;
                        Console.WriteLine("Um navio foi atingido.");
                    }
                    else 
                    {
                        Console.WriteLine("tiro caiu no mar.");
                    }
                }
                else
                {
                    Console.WriteLine("O inimigo atirou fora do mapa.");
                }

                byte[] pacoteResposta = CriarPacoteProtocolo(2, TOKEN, status, usarCripto);
                udpClient.Send(pacoteResposta, pacoteResposta.Length, remetente);

                meuTurno = true;
            }
        }

        Console.WriteLine(meusPontos >= totalNavios ? "\nVitória!" : "\nDerrota!");
    }

    static string ProcessarPayload(string dados)
    {
        char[] charArray = dados.ToCharArray();
        for (int i = 0; i < charArray.Length; i++)
        {
            charArray[i] = (char)(charArray[i] ^ CHAVE_XOR);
        }
        return new string(charArray);
    }

    static uint CalcularChecksum(byte[] dados)
    {
        uint soma = 0;
        for (int i = 0; i < dados.Length; i++) soma += dados[i];
        return soma;
    }

    static byte[] CriarPacoteProtocolo(byte tipoMsg, string token, string payloadTxt, bool usarCripto)
    {
        using MemoryStream ms = new MemoryStream();
        using BinaryWriter writer = new BinaryWriter(ms);
        
        if (usarCripto) payloadTxt = ProcessarPayload(payloadTxt);
        
        byte[] payload = Encoding.UTF8.GetBytes(payloadTxt);
        byte[] tokenBytes = Encoding.UTF8.GetBytes(token.PadRight(16).Substring(0, 16));
        uint checksum = CalcularChecksum(payload);

        writer.Write((ushort)0x424E);
        writer.Write((byte)1);
        writer.Write(tipoMsg);
        writer.Write((uint)1);
        writer.Write((ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        writer.Write(tokenBytes);
        writer.Write(checksum);
        writer.Write((uint)payload.Length);
        writer.Write(payload);

        return ms.ToArray();
    }

    static string LerPayloadProtocolo(byte[] dados, bool usarCripto)
    {
        using MemoryStream ms = new MemoryStream(dados);
        using BinaryReader reader = new BinaryReader(ms);

        reader.BaseStream.Seek(32, SeekOrigin.Begin);
        uint checksumRecebido = reader.ReadUInt32();
        uint tamanhoPayload = reader.ReadUInt32();
        byte[] payloadBytes = reader.ReadBytes((int)tamanhoPayload);

        uint checksumCalculado = CalcularChecksum(payloadBytes);
        if (checksumRecebido != checksumCalculado)
        {
            Console.WriteLine("\n[AVISO] Falha no Checksum! O pacote foi corrompido na rede.");
        }

        string payloadTxt = Encoding.UTF8.GetString(payloadBytes);
        if (usarCripto) payloadTxt = ProcessarPayload(payloadTxt);
        
        return payloadTxt;
    }
}

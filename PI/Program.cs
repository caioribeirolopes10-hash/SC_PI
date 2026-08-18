using System;
using System.IO.Ports;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

class Program
{
    // CONFIGURAÇÕES
    // A COM será encontrada automaticamente
    static string portaCom = "";
    // Velocidade da comunicação
    static int baudRate = 115200;
    // Resistência do trimpot
    static double resistenciaMin = 0;
    static double resistenciaMax = 4000;
    // Capacidade máxima do reservatório
    static double capacidadeMaxLitros = 100;
    // API da IA Python
    static string urlIA = "http://127.0.0.1:5000/prever";
    // API Node que abastece a interface Web
    static string urlServidor = "http://localhost:3000/api/agua";
    // Porta serial
    static SerialPort porta;

    // DADOS ATUAIS
    static double ultimaResistencia = 0;
    static double ultimoNivelLitros = 0;
    static double ultimoNivelPorcentagem = 0;
    static string ultimaClassificacao = "Aguardando";

    static void Main()
    {
        Console.WriteLine("======================================");
        Console.WriteLine(" SISTEMA DE MONITORAMENTO DE ÁGUA");
        Console.WriteLine("======================================");
        Console.WriteLine();

        // Procura automaticamente a porta
        if (!EncontrarPorta())
        {
            Console.WriteLine("Nenhuma porta COM encontrada.");
            Console.ReadLine();
            return;
        }

        // Configura comunicação
        ConfigurarComunicacaoSTM32();

        // Conecta
        if (!ConectarSTM32())
        {
            return;
        }
        Console.WriteLine();
        Console.WriteLine("Sistema iniciado.");
        Console.WriteLine("Aguardando dados do STM32...");
        Console.WriteLine();
        Console.ReadLine();
    }

    // 1. DETECTAR PORTA COM AUTOMATICAMENTE
    static bool EncontrarPorta()
    {
        string[] portas =SerialPort.GetPortNames();
        Console.WriteLine("Portas COM encontradas:");

        if (portas.Length == 0)
        {
            return false;
        }

        foreach (string p in portas)
        {
            Console.WriteLine("- " + p);
        }

        // Se houver apenas uma porta,
        // usa automaticamente
        if (portas.Length == 1)
        {
            portaCom = portas[0];
            Console.WriteLine();
            Console.WriteLine("STM32 selecionado: " + portaCom);
            return true;
        }

        // Se houver várias portas,
        // pede para escolher
        Console.WriteLine();
        Console.WriteLine("Escolha a porta do STM32:");

        for (int i = 0; i < portas.Length; i++)
        {
            Console.WriteLine($"{i + 1} - {portas[i]}");
        }

        Console.Write("Número: ");
        string entrada = Console.ReadLine();
        if (int.TryParse(entrada,out int escolha))
        {
            if (escolha >= 1 &&escolha <= portas.Length)
            {
                portaCom =portas[escolha - 1];
                return true;
            }
        }
        return false;
    }

    // 2. CONFIGURAR STM32
    static void ConfigurarComunicacaoSTM32()
    {
        porta = new SerialPort(portaCom,baudRate,Parity.None,8,StopBits.One);
        // Receber dados automaticamente
        porta.DataReceived += ReceberDadosSTM32;
    }

    // 3. CONECTAR STM32
    static bool ConectarSTM32()
    {
        try
        {
            porta.Open();
            Console.WriteLine();
            Console.WriteLine("STM32 conectado!");
            Console.WriteLine("COM: " + portaCom);
            Console.WriteLine("Baud Rate: " + baudRate);
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine("Erro ao conectar:");
            Console.WriteLine(ex.Message);
            return false;
        }
    }

    // 4. RECEBER HEX DO STM32

    static async void ReceberDadosSTM32(object sender,SerialDataReceivedEventArgs e)
    {
        try
        {
            // Espera exatamente 2 bytes
            if (porta.BytesToRead < 2)
            {
                return;
            }

            byte[] dados = new byte[2];
            porta.Read(dados,0,2);

            // Mostra os bytes em hexadecimal
            Console.WriteLine($"HEX recebido: {dados[0]:X2} {dados[1]:X2}");
            // Converte os dois bytes
            // para resistência
            ushort resistencia =(ushort)((dados[0] << 8)|dados[1]);
            Console.WriteLine($"Resistência: {resistencia} Ω");
            await ProcessarResistencia(resistencia);
        }
        catch (Exception ex)
        {
            Console.WriteLine("Erro ao receber dados:");
            Console.WriteLine(ex.Message);
        }
    }

    // 5. PROCESSAR RESISTÊNCIA
    static async Task ProcessarResistencia(double resistencia)
    {
        ultimaResistencia =resistencia;
        // Ω → Litros
        double litros =
            ConverterOhmsParaLitros(resistencia);
        // Litros → %
        double porcentagem =ConverterLitrosParaPorcentagem(litros);

        ultimoNivelLitros =litros;
        ultimoNivelPorcentagem =porcentagem;

        Console.WriteLine($"Água: {litros:F2} L");
        Console.WriteLine($"Nível: {porcentagem:F1}%");

        // Cria JSON
        string json =CriarJsonIA(resistencia,litros,porcentagem);

        Console.WriteLine("JSON enviado para IA:");
        Console.WriteLine(json);
        // C# → IA
        await EnviarParaIA(json);
    }

    // 6. Ω → LITROS

    static double ConverterOhmsParaLitros(
        double resistencia)
    {
        if (resistencia < resistenciaMin)
        {
            resistencia =resistenciaMin;
        }

        if (resistencia > resistenciaMax)
        {
            resistencia =resistenciaMax;
        }
        double litros =((resistencia - resistenciaMin)/(resistenciaMax - resistenciaMin))*capacidadeMaxLitros;
        return Math.Round(litros,2);
    }

    // 7. LITROS → PORCENTAGEM

    static double ConverterLitrosParaPorcentagem(double litros)
    {
        if (litros < 0)
        {
            litros = 0;
        }

        if (litros > capacidadeMaxLitros)
        {
            litros = capacidadeMaxLitros;
        }

        double porcentagem =(litros / capacidadeMaxLitros)* 100;
        return Math.Round(porcentagem,1);
    }

    // 8. CRIAR JSON PARA IA
    static string CriarJsonIA(double resistencia,double litros,double porcentagem)
    {
        var dados = new {resistencia = resistencia,litros = litros,nivel = porcentagem};
        return JsonSerializer.Serialize(dados);
    }

    // 9. C# → IA
 
    static async Task EnviarParaIA(string json)
    {
        try
        {
            using HttpClient cliente =new HttpClient();
            StringContent conteudo =new StringContent(json,Encoding.UTF8,"application/json");
            HttpResponseMessage resposta =await cliente.PostAsync(urlIA,conteudo);
            if (resposta.IsSuccessStatusCode)
            {
                string resultado =await resposta.Content.ReadAsStringAsync();
                Console.WriteLine("Resposta da IA: "+ resultado);
                await ProcessarRespostaIA(resultado);
            }
            else
            {
                Console.WriteLine("Erro na IA: "+ resposta.StatusCode);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine( "Erro ao enviar para IA:");
            Console.WriteLine(ex.Message);
        }
    }

    // 10. IA → C#
    static async Task ProcessarRespostaIA(string json)
    {
        try
        {
            using JsonDocument documento =JsonDocument.Parse(json);
            JsonElement raiz =documento.RootElement;

            if (raiz.TryGetProperty("classificacao",out JsonElement classificacao))
            {
                ultimaClassificacao =classificacao.GetString()??"Desconhecido";
                Console.WriteLine("Classificação: "+ ultimaClassificacao);
                await EnviarParaServidorWeb();
            }
            else
            {
                Console.WriteLine("A IA não retornou o campo 'classificacao'.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Erro ao interpretar IA:");
            Console.WriteLine(ex.Message);
        }
    }

    // 11. C# → NODE → HTML
    static async Task EnviarParaServidorWeb()
    {
        try
        {
            var dados =new
            {
                nivel =ultimoNivelPorcentagem,
                classificacao =ultimaClassificacao
            };

            string json =JsonSerializer.Serialize(dados);

            using HttpClient cliente =new HttpClient();
            using StringContent conteudo =new StringContent(
                json,
                Encoding.UTF8,
                "application/json"
            );

            HttpResponseMessage resposta =await cliente.PostAsync(
                urlServidor,
                conteudo
            );

            if (resposta.IsSuccessStatusCode)
            {
                Console.WriteLine("Dados enviados para a interface Web.");
            }
            else
            {
                Console.WriteLine("Erro no servidor Web: "+ resposta.StatusCode);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Erro ao enviar dados para o servidor Web:");
            Console.WriteLine(ex.Message);
        }

    }
}
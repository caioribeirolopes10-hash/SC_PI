using System;
using System.IO.Ports;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Globalization;
using System.Threading.Tasks;
class Program
{
    // CONFIGURAÇÕES
    // STM32
    static string portaCom = "COM3";
    static int baudRate = 115200;
    // Resistência do trimpot
    static double resistenciaMin = 0;
    static double resistenciaMax    = 4000;
    // Capacidade máxima do reservatório
    // ALTERE PARA A CAPACIDADE REAL DO SEU RESERVATÓRIO
    static double capacidadeMaxLitros = 100;
    // API da IA Python
    static string urlIA = "http://127.0.0.1:5000/prever";
    // Servidor que entrega os dados para o SEU JavaScript
    static string urlServidor = "http://localhost:3000";
    // Porta serial
    static SerialPort porta;

    // DADOS ATUAIS

    static double ultimaResistencia = 0;
    static double ultimoNivelLitros = 0;
    static double ultimoNivelPorcentagem = 0;
    static string ultimaClassificacao = "Aguardando";

    // MAIN
    static void Main()
    {
        Console.WriteLine("======================================");
        Console.WriteLine(" SISTEMA DE MONITORAMENTO DE ÁGUA");
        Console.WriteLine("======================================");
        Console.WriteLine();
        // Configura STM32
        ConfigurarComunicacaoSTM32();
        // Conecta ao STM32
        if (!ConectarSTM32())
        {
            return;
        }
        // Inicia comunicação com o seu JS
        IniciarServidorParaJS();

        Console.WriteLine();
        Console.WriteLine("Sistema iniciado.");
        Console.WriteLine("Aguardando dados do STM32...");
        Console.WriteLine();
        // Mantém o programa aberto
        Console.ReadLine();
    }

    // 1. STM32 → C#

    static void ConfigurarComunicacaoSTM32()
{
    porta = new SerialPort(
        portaCom,
        baudRate,
        Parity.None,
        8,
        StopBits.One
    );

    // Quando chegar um dado do STM32
    porta.DataReceived += ReceberDadosSTM32;
}

    static bool ConectarSTM32()
    {
        try
        {
            porta.Open();
            Console.WriteLine("STM32 conectado.");
            Console.WriteLine("COM: " + portaCom);
            Console.WriteLine("Baud Rate: " + baudRate);
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine("Erro ao conectar ao STM32:");
            Console.WriteLine(ex.Message);
            return false;
        }
    }

    // 2. RECEBER DADO DO STM32

    static async void ReceberDadosSTM32(object sender,SerialDataReceivedEventArgs e)
    {
        try
        {
            // O STM32 deve enviar uma resistência por linha
            string dados = porta.ReadLine().Trim();
            Console.WriteLine("Recebido do STM32: " + dados);

            // Converte o texto recebido para número
            if (double.TryParse(dados,NumberStyles.Any,CultureInfo.InvariantCulture,out double resistencia))
            {
                await ProcessarResistencia(resistencia);
            }
            else
            {
                Console.WriteLine("O valor recebido não é uma resistência válida.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Erro ao receber dados do STM32:");
            Console.WriteLine(ex.Message);
        }
    }

    // 3. PROCESSAR RESISTÊNCIA
  
    static async Task ProcessarResistencia(double resistencia)
    {
        // Guarda resistência
        ultimaResistencia = resistencia;
        Console.WriteLine($"Resistência: {resistencia:F2} Ω");
        // Ω → Litros
        double litros = ConverterOhmsParaLitros(resistencia);
        // Litros → %
        double porcentagem = ConverterLitrosParaPorcentagem(litros);

        // Guarda valores
        ultimoNivelLitros = litros;
        ultimoNivelPorcentagem = porcentagem;
        Console.WriteLine($"Água: {litros:F2} L");
        Console.WriteLine($"Nível: {porcentagem:F1}%");

        // Cria JSON para IA
        string json = CriarJsonIA(resistencia, litros, porcentagem);
        Console.WriteLine("JSON enviado para IA:");
        Console.WriteLine(json);
        // C# → IA
        await EnviarParaIA(json);
    }

    // 4. Ω → LITROS
    static double ConverterOhmsParaLitros(
        double resistencia)
    {
        // Limita resistência mínima
        if (resistencia < resistenciaMin)
        {
            resistencia = resistenciaMin;
        }
        // Limita resistência máxima
        if (resistencia > resistenciaMax)
        {
            resistencia = resistenciaMax;
        }
        // Conversão proporcional
        double litros =((resistencia - resistenciaMin) /(resistenciaMax - resistenciaMin))* capacidadeMaxLitros;return Math.Round(litros, 2);}

    // 5. LITROS → PORCENTAGEM
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
        double porcentagem = (litros / capacidadeMaxLitros) * 100;

        return Math.Round(porcentagem, 1);
    }

    // 6. CRIAR JSON PARA IA
    static string CriarJsonIA(double resistencia, double litros, double porcentagem)
    {
        var dados = new
        {
            resistencia = resistencia, litros = litros, nivel = porcentagem
        };
        return JsonSerializer.Serialize(dados);
    }

    // 7. C# → IA
    static async Task EnviarParaIA(string json)
    {
        try
        {
            using HttpClient cliente = new HttpClient();
            StringContent conteudo = new StringContent(json,Encoding.UTF8,"application/json");
            HttpResponseMessage resposta = await cliente.PostAsync(urlIA,conteudo);

            // Verifica se a API respondeu corretamente
            if (resposta.IsSuccessStatusCode)
            {
                string resultado = await resposta.Content.ReadAsStringAsync();
                Console.WriteLine("Resposta da IA: "+ resultado);
                ProcessarRespostaIA(resultado);
            }
            else
            {
                Console.WriteLine("Erro na IA: "+ resposta.StatusCode);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Erro ao enviar para IA:");
            Console.WriteLine(ex.Message);
        }
    }

    // 8. IA → C#
    static void ProcessarRespostaIA(
        string json)
    {
        try
        {
            using JsonDocument documento =JsonDocument.Parse(json);
            JsonElement raiz = documento.RootElement;
            // Pega a classificação retornada pela IA
            if (raiz.TryGetProperty("classificacao",out JsonElement classificacao))
            {
                ultimaClassificacao =classificacao.GetString()?? "Desconhecido";
                Console.WriteLine("Classificação: "+ ultimaClassificacao);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Erro ao interpretar resposta da IA:");
            Console.WriteLine(ex.Message);
        }
    }
    // 9. C# → SEU JAVASCRIPT
    static void IniciarServidorParaJS()
    {
        HttpListener servidor = new HttpListener();
        servidor.Prefixes.Add(urlServidor);

        try
        {
            servidor.Start();
            Console.WriteLine();
            Console.WriteLine("Comunicação com o JavaScript iniciada.");

            Console.WriteLine("Endpoint: http://localhost:3000/api/agua");
            Task.Run(async () =>
            {
                while (true)
                {
                    HttpListenerContext contexto =await servidor.GetContextAsync();
                    ResponderAoJavaScript(contexto);
                }
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine("Erro na comunicação com o JavaScript:");
            Console.WriteLine(ex.Message);
        }
    }

    // 10. ENVIAR JSON PARA O SEU JS
    static void ResponderAoJavaScript(
        HttpListenerContext contexto)
    {
        try
        {
            string caminho =contexto.Request.Url?.AbsolutePath?? "/";
            // Seu JS fará uma requisição para:
            // http://localhost:3000/api/agua

            if (caminho == "/api/agua")
            {
                string json =CriarJsonParaSite();
                byte[] dados =Encoding.UTF8.GetBytes(json);

                contexto.Response.ContentType ="application/json";
                contexto.Response.Headers.Add("Access-Control-Allow-Origin","*");
                contexto.Response.ContentLength64 =dados.Length;
                contexto.Response.OutputStream.Write(dados,0,dados.Length);
                contexto.Response.OutputStream.Close();
            }
            else
            {
                contexto.Response.StatusCode = 404;
                contexto.Response.Close();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Erro ao enviar dados para o JS:");
            Console.WriteLine(ex.Message);
            contexto.Response.StatusCode = 500;
            contexto.Response.Close();
        }
    }

    // 11. JSON QUE O SEU JS RECEBE
    static string CriarJsonParaSite()
    {
        var dados = new
        {
            resistencia = ultimaResistencia, litros = ultimoNivelLitros, nivel = ultimoNivelPorcentagem, classificacao = ultimaClassificacao
        };

        return JsonSerializer.Serialize(dados);
    }
}
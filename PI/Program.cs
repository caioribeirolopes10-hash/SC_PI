using System;
using System.IO.Ports;
using System.Net;
using System.Text;
using System.Text.Json;

class Program
{
// Altere para a COM do seu STM32
    static string portaCom = "COM3";

    // Velocidade configurada no STM32
    static int baudRate = 115200;

    // Resistência mínima e máxima do trimpot
    static double resistenciaMin = 0;
    static double resistenciaMax = 4000;

    // Endereço da API Python da IA
    static string urlIA = "http://127.0.0.1:5000/prever";

    static SerialPort porta;

    // Último resultado recebido pela IA
    static string ultimoNivel = "Aguardando";

    // Último valor em porcentagem
    static double ultimoNivelAgua = 0;

    // Última resistência recebida
    static double ultimaResistencia = 0;

    static void Main()
    {
        Console.WriteLine(" SISTEMA DE MONITORAMENTO DE ÁGUA");
        Console.WriteLine();

        // Cria a porta serial
        porta = new SerialPort(portaCom,baudRate,Parity.None,8,StopBits.One);

        // Evento chamado quando o STM32 enviar dados
        porta.DataReceived += ReceberDadosSTM32;

        try
        {
            // Abre comunicação com STM32
            porta.Open();

            Console.WriteLine("STM32 conectado.");
            Console.WriteLine("Porta: " + portaCom);
            Console.WriteLine("Baud Rate: " + baudRate);
            Console.WriteLine();
            Console.WriteLine("Aguardando dados...");
            Console.WriteLine();

            // Mantém o programa funcionando
            IniciarServidor();
            Console.ReadLine();
        }
        catch (Exception ex)
        {
            Console.WriteLine("Erro ao conectar ao STM32:");
            Console.WriteLine(ex.Message);
        }
    }

    // RECEBE DADOS DO STM32
    
    static async void ReceberDadosSTM32(object sender, SerialDataReceivedEventArgs e)
    {
        try
        {
            // Lê uma linha enviada pelo STM32
            string dados = porta.ReadLine().Trim();

            Console.WriteLine("Recebido do STM32: " + dados);

            // Tenta transformar o texto em número
            if (double.TryParse(dados,System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture,out double resistencia))
            {
                // Guarda resistência
                ultimaResistencia = resistencia;
                // Converte resistência para nível de água
                double nivelAgua = ConverterParaPorcentagem(resistencia);
                // Guarda nível
                ultimoNivelAgua = nivelAgua;
                Console.WriteLine($"Resistência: {resistencia:F2} Ω");
                Console.WriteLine($"Nível da água: {nivelAgua:F1}%");
                // Envia para a IA
                await EnviarParaIA(nivelAgua);
                Console.WriteLine();
            }
            else
            {
                Console.WriteLine(
                    "Valor recebido não é numérico."
                );
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                "Erro ao receber dados: " + ex.Message
            );
        }
    }
    // CONVERSÃO RESISTÊNCIA → PORCENTAGEM
    static double ConverterParaPorcentagem(double resistencia)
    {
        // Impede valores abaixo do mínimo
        if (resistencia < resistenciaMin)
        {
            resistencia = resistenciaMin;
        }
        // Impede valores acima do máximo
        if (resistencia > resistenciaMax)
        {
            resistencia = resistenciaMax;
        }
        // Converte 0–4000 Ω para 0–100%
        double nivel =
            ((resistencia - resistenciaMin) /
            (resistenciaMax - resistenciaMin)) * 100;

        return Math.Round(nivel, 1);
    }
    // ENVIA JSON PARA A IA
    static async System.Threading.Tasks.Task EnviarParaIA(
        double nivelAgua)
    {
        try
        {
            // Monta o JSON
            var dados = new { nivel = nivelAgua };

            string json = JsonSerializer.Serialize(dados);
            Console.WriteLine("JSON enviado para IA:");
            Console.WriteLine(json);
            using HttpClient cliente = new HttpClient();

            // Define o conteúdo
            StringContent conteudo = new StringContent(json,Encoding.UTF8,"application/json");
            // Envia POST para a API Python
            HttpResponseMessage resposta =
                await cliente.PostAsync(urlIA,conteudo);

            // Verifica se a API respondeu corretamente
            if (resposta.IsSuccessStatusCode)
            {
                // Lê resposta da IA
                string resultado = await resposta.Content.ReadAsStringAsync();
                Console.WriteLine("Resposta da IA:");
                Console.WriteLine(resultado);
                ProcessarRespostaIA(resultado);
            }
            else
            {
                Console.WriteLine( "Erro na API da IA: " + resposta.StatusCode);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                "Erro ao enviar para IA: " +
                ex.Message
            );
        }
    }
    // PROCESSA RESPOSTA DA IA
    static void ProcessarRespostaIA(string json)
    {
        try
        {
            using JsonDocument documento = JsonDocument.Parse(json);
            JsonElement raiz =documento.RootElement;
            // Esperamos que a IA responda:
            //     "nivel": 65,
            //     "classificacao": "Médio"

            if (raiz.TryGetProperty(
                "classificacao",out JsonElement classificacao))
            {
                ultimoNivel =classificacao.GetString() ?? "Desconhecido";
                Console.WriteLine("Classificação da IA: " +ultimoNivel
                );
            }
        }
        catch
        {
            Console.WriteLine( "Não foi possível interpretar a resposta da IA." );
        }
    }
    // SERVIDOR PARA O SITE
    static void IniciarServidor()
    {
        HttpListener servidor = new HttpListener();
        servidor.Prefixes.Add("http://localhost:8080/");

        try
        {
            servidor.Start();
            Console.WriteLine("Servidor C# iniciado:");
            Console.WriteLine("http://localhost:8080/");

            System.Threading.Tasks.Task.Run(
                async () =>
                {
                    while (true)
                    {
                        HttpListenerContext contexto =await servidor.GetContextAsync();
                        ProcessarRequisicaoSite(contexto);
                    }
                }
            );
        }
        catch (Exception ex)
        {
            Console.WriteLine("Erro ao iniciar servidor:" );
            Console.WriteLine(ex.Message);
        }
    }
    // RESPONDE AO SITE
    static void ProcessarRequisicaoSite(
        HttpListenerContext contexto)
    {
        try
        {
            string caminho =contexto.Request.Url?.AbsolutePath ?? "/";

            if (caminho == "/api/agua")
            {
                // JSON que será enviado para o site
                var resposta = new
                {
                    resistencia = ultimaResistencia,
                    nivel = ultimoNivelAgua,
                    classificacao = ultimoNivel
                };

                string json = JsonSerializer.Serialize(resposta);
                byte[] buffer =Encoding.UTF8.GetBytes(json);
                contexto.Response.ContentType ="application/json";
                contexto.Response.Headers.Add("Access-Control-Allow-Origin","*" );
                contexto.Response.ContentLength64 =buffer.Length;
                contexto.Response.OutputStream.Write(buffer,0,buffer.Length);
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
            Console.WriteLine("Erro ao responder ao site: " +ex.Message);
            contexto.Response.StatusCode = 500;
            contexto.Response.Close();
        }
    }
}
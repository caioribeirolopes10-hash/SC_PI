    using System;
    using System.IO.Ports;
    using System.Text;
    using System.Text.Json;
    using System.Threading.Tasks;

    class Program
    {
        // CONFIGURAÇÕES
        static string portaCom = "";
        static int baudRate = 115200;
        static double resistenciaMin = 0;
        static double resistenciaMax = 4000;
        static double capacidadeMaxLitros = 100;
        static string urlIA = "http://127.0.0.1:5000/prever";
        static string urlServidor = "http://localhost:3000/api/agua";
        static SerialPort? porta = null;

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

            // DETECTAR PORTA COM
            if (!EncontrarPorta())
            {
                Console.WriteLine("Nenhuma porta COM encontrada.");
                Console.ReadLine();
                return;
            }

            // CONFIGURAR STM32
            ConfigurarComunicacaoSTM32();

            // CONECTAR STM32
            if (!ConectarSTM32())
            {
                Console.ReadLine();
                return;
            }

            Console.WriteLine();
            Console.WriteLine("Sistema iniciado.");
            Console.WriteLine("Aguardando dados do STM32...");
            Console.WriteLine();

            Console.ReadLine();
        }

        // DETECTAR PORTA COM
        static bool EncontrarPorta()
        {
            string[] portas = SerialPort.GetPortNames();

            Console.WriteLine("Portas COM encontradas:");

            if (portas.Length == 0)
            {
                return false;
            }

            foreach (string p in portas)
            {
                Console.WriteLine("- " + p);
            }

            if (portas.Length == 1)
            {
                portaCom = portas[0];

                Console.WriteLine();
                Console.WriteLine("STM32 selecionado: " + portaCom);

                return true;
            }

            Console.WriteLine();
            Console.WriteLine("Escolha a porta do STM32:");

            for (int i = 0; i < portas.Length; i++)
            {
                Console.WriteLine($"{i + 1} - {portas[i]}");
            }

            Console.Write("Número: ");

            string? entrada = Console.ReadLine();

            if (int.TryParse(entrada, out int escolha))
            {
                if (escolha >= 1 && escolha <= portas.Length)
                {
                    portaCom = portas[escolha - 1];

                    Console.WriteLine("Porta selecionada: " + portaCom);

                    return true;
                }
            }

            return false;
        }

        // CONFIGURAR STM32
        static void ConfigurarComunicacaoSTM32()
        {
            porta = new SerialPort(portaCom, baudRate, Parity.None, 8, StopBits.One);
            porta.DataReceived += ReceberDadosSTM32;
        }

        // CONECTAR STM32
        static bool ConectarSTM32()
        {
            try
            {
                if (porta == null)
                {
                    Console.WriteLine("Porta serial não configurada.");
                    return false;
                }

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

        // STM32 → C#
        static async void ReceberDadosSTM32(object sender, SerialDataReceivedEventArgs e)
    {
        try
        {
            if (porta == null || !porta.IsOpen)
            {
                return;
            }

            while (porta.BytesToRead >= 6)
            {
                byte[] mensagem = new byte[6];

                int quantidade = porta.Read(mensagem, 0, 6);

                if (quantidade != 6)
                {
                    return;
                }

                Console.WriteLine(
                    $"HEX recebido: {mensagem[0]:X2} {mensagem[1]:X2} {mensagem[2]:X2} {mensagem[3]:X2} {mensagem[4]:X2} {mensagem[5]:X2}"
                );

                if (mensagem[0] != 0x40 ||
                    mensagem[1] != 0x01 ||
                    mensagem[2] != 0x7C ||
                    mensagem[5] != 0x23)
                {
                    Console.WriteLine("Pacote inválido.");
                    continue;
                }

                ushort valorRecebido =
                    (ushort)((mensagem[3] << 8) | mensagem[4]);

                Console.WriteLine($"Valor ADC recebido: {valorRecebido}");

                await ProcessarValorRecebido(valorRecebido);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Erro ao receber dados:");
            Console.WriteLine(ex.Message);
        }
    }

        // PROCESSAR RESISTÊNCIA
        static async Task ProcessarValorRecebido(double valorRecebido)
        {
            double resistencia = valorRecebido;

            if (resistencia < resistenciaMin)
            {
                resistencia = resistenciaMin;
            }

            if (resistencia > resistenciaMax)
            {
                resistencia = resistenciaMax;
            }

            ultimaResistencia = resistencia;

            double litros = ConverterOhmsParaLitros(resistencia);
            double porcentagem = ConverterLitrosParaPorcentagem(litros);

            ultimoNivelLitros = litros;
            ultimoNivelPorcentagem = porcentagem;

            Console.WriteLine($"Resistência: {resistencia:F2} Ω");
            Console.WriteLine($"Água: {litros:F2} L");
            Console.WriteLine($"Nível: {porcentagem:F2}%");
            Console.WriteLine();

            string json = CriarJsonIA(resistencia, litros, porcentagem);

            Console.WriteLine("JSON enviado para IA:");
            Console.WriteLine(json);
            Console.WriteLine();

            await EnviarParaIA(json);
        }

        // OHMS → LITROS
        static double ConverterOhmsParaLitros(double resistencia)
        {
            if (resistencia < resistenciaMin)
            {
                resistencia = resistenciaMin;
            }

            if (resistencia > resistenciaMax)
            {
                resistencia = resistenciaMax;
            }

            double litros = ((resistencia - resistenciaMin) / (resistenciaMax - resistenciaMin)) * capacidadeMaxLitros;

            return Math.Round(litros, 2);
        }

        // LITROS → PORCENTAGEM
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

            double porcentagem = (litros / capacidadeMaxLitros) * 100.0;

            return Math.Round(porcentagem, 2);
        }

        // C# → JSON
        static string CriarJsonIA(double resistencia, double litros, double porcentagem)
        {
            var dados = new
            {
                resistencia = resistencia,
                litros = litros,
                nivel = porcentagem
            };

            return JsonSerializer.Serialize(dados);
        }

        // C# → IA
        static async Task EnviarParaIA(string json)
        {
            try
            {
                using HttpClient cliente = new HttpClient();

                using StringContent conteudo = new StringContent(
                    json,
                    Encoding.UTF8,
                    "application/json"
                );

                HttpResponseMessage resposta = await cliente.PostAsync(urlIA, conteudo);

                if (resposta.IsSuccessStatusCode)
                {
                    string resultado = await resposta.Content.ReadAsStringAsync();

                    Console.WriteLine("Resposta da IA: " + resultado);

                    await ProcessarRespostaIA(resultado);
                }
                else
                {
                    Console.WriteLine("Erro na IA: " + resposta.StatusCode);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Erro ao enviar para IA:");
                Console.WriteLine(ex.Message);
            }
        }

        // IA → C#
        static async Task ProcessarRespostaIA(string json)
        {
            try
            {
                using JsonDocument documento = JsonDocument.Parse(json);

                JsonElement raiz = documento.RootElement;

                if (raiz.TryGetProperty("classificacao", out JsonElement classificacao))
                {
                    ultimaClassificacao = classificacao.GetString() ?? "Desconhecido";

                    Console.WriteLine("Classificação: " + ultimaClassificacao);

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

        // C# → NODE → HTML
        static async Task EnviarParaServidorWeb()
        {
            try
            {
                var dados = new
                {
                    nivel = ultimoNivelPorcentagem,
                    classificacao = ultimaClassificacao
                };

                string json = JsonSerializer.Serialize(dados);

                using HttpClient cliente = new HttpClient();

                using StringContent conteudo = new StringContent(
                    json,
                    Encoding.UTF8,
                    "application/json"
                );

                HttpResponseMessage resposta = await cliente.PostAsync(
                    urlServidor,
                    conteudo
                );

                if (resposta.IsSuccessStatusCode)
                {
                    Console.WriteLine("Dados enviados para a interface Web.");
                }
                else
                {
                    Console.WriteLine("Erro no servidor Web: " + resposta.StatusCode);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Erro ao enviar dados para o servidor Web:");
                Console.WriteLine(ex.Message);
            }
        }
    }
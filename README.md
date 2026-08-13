INTEGRANTES: CAIO LOPES E EVELYN DANDARAH.
LINK DO VÍDEO: 

Este projeto foi desenvolvido como parte do **Projeto Integrado**, com o objetivo de desenvolver um sistema completo de **aquisição, processamento, classificação e visualização de dados**.

O sistema simula o monitoramento do **nível de água de um reservatório**. Para representar o sensor de nível, é utilizado um **trimpot (potenciômetro)** conectado ao microcontrolador **STM32F103C8 (Blue Pill)**.

A posição do trimpot representa diferentes níveis de água:

* 🟦 **Vazio** — nível baixo de água;
* 🟨 **Normal** — nível adequado de água;
* 🟩 **Cheio** — nível alto de água.

A leitura realizada pelo STM32 é enviada para o computador através de **USB CDC (Porta COM)**. Uma aplicação em **C#** recebe e organiza os dados, converte as informações para **JSON** e envia as medições para uma **API REST**.

O servidor encaminha os dados para um modelo de **Inteligência Artificial**, que classifica automaticamente o nível da água. Por fim, os resultados são apresentados em uma **interface Web**.
---
O projeto tem como principais objetivos:

* Realizar a leitura de um sensor analógico utilizando o STM32;
* Utilizar um trimpot para simular o nível da água;
* Enviar periodicamente as medições para o computador;
* Estabelecer comunicação através de uma Porta COM;
* Processar e organizar os dados recebidos;
* Converter as medições para JSON;
* Enviar os dados para uma API REST;
* Utilizar Inteligência Artificial para classificar o nível da água;
* Exibir as informações em uma interface Web;
* Manter um histórico das medições.

O projeto integra conceitos de **Sistemas Embarcados, Linguagens de Programação, Desenvolvimento de Aplicativos e Inteligência Artificial**.
---
O funcionamento geral pode ser representado pelo seguinte fluxo:
┌─────────────────┐
│     TRIMPOT     │
│ Simula o nível  │
│     da água     │
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│      STM32      │
│  Leitura ADC    │
└────────┬────────┘
         │
         │ USB CDC / Porta COM
         ▼
┌─────────────────┐
│       C#        │
│ Recebe e trata  │
│     os dados     │
└────────┬────────┘
         │
         │ JSON / HTTP
         ▼
┌─────────────────┐
│    API REST     │
│    Servidor     │
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│       IA        │
│  Classificação  │
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│   Interface     │
│       Web       │
└─────────────────┘
```
---
O **STM32F103C8** é responsável por realizar a leitura analógica do trimpot.

O trimpot funciona como uma representação do nível de água. Ao alterar sua posição, o valor analógico enviado ao ADC do STM32 também é alterado.

O valor obtido pelo ADC é utilizado para determinar o estado do reservatório:

| Faixa da leitura    | Estado    |
| ------------------- | --------- |
| Valor baixo         | 🟦 Vazio  |
| Valor intermediário | 🟨 Normal |
| Valor alto          | 🟩 Cheio  |

> As faixas exatas utilizadas pelo projeto devem ser definidas no código de acordo com a calibração do trimpot.

O STM32 realiza a conversão do sinal analógico através do **ADC (Conversor Analógico-Digital)** e envia periodicamente o valor obtido através da comunicação **USB CDC / Porta COM**.

Cada transmissão contém, no mínimo, o valor medido pelo sensor.

A aplicação desenvolvida em **C#**

Suas principais responsabilidades são:

1. Estabelecer comunicação com a Porta COM;
2. Receber continuamente os valores enviados pelo STM32;
3. Organizar e tratar os dados recebidos;
4. Converter as informações para JSON;
5. Enviar as medições para a API através de requisições HTTP.

Exemplo de dado recebido do STM32:

2048
```
Após o tratamento, a informação pode ser organizada em JSON:

```json
{
    "valor": 2048
}
```
Esse JSON é então enviado para o servidor.
---
A API REST é responsável por receber as medições enviadas pela aplicação C#.

O servidor possui como principais funções:

* Receber as leituras;
* Processar as informações;
* Encaminhar os dados para o módulo de Inteligência Artificial;
* Receber o resultado da classificação;
* Retornar a classificação para o cliente;
* Disponibilizar os dados necessários para a interface Web.

A comunicação entre a aplicação e a API utiliza **HTTP e JSON**.
---
O projeto utiliza um modelo de **Machine Learning** para realizar a classificação automática das leituras.

A partir do valor recebido, o modelo determina em qual categoria o nível da água se encontra.

       NÍVEL DA ÁGUA

          ┌─────────┐
          │  CHEIO  │
          └─────────┘
              ▲
              │
          ┌─────────┐
          │ NORMAL  │
          └─────────┘
              ▲
              │
          ┌─────────┐
          │  VAZIO  │
          └─────────┘
```

O objetivo é que cada nova leitura recebida seja classificada automaticamente pelo modelo.

A interface Web apresenta os resultados do monitoramento de forma visual.

A página deve apresentar:

* **Valor atual da leitura**;
* **Classificação atual**;
* **Histórico das últimas medições**;
* **Horário da última atualização**;
* **Indicação visual do estado atual do reservatório**.

Exemplo:
╔══════════════════════════════════╗
║       MONITORAMENTO DA ÁGUA      ║
╠══════════════════════════════════╣
║                                  ║
║       Nível atual: 2048          ║
║                                  ║
║       Estado: NORMAL              ║
║                                  ║
║       Última atualização: 10:30  ║
║                                  ║
╠══════════════════════════════════╣
║      HISTÓRICO DAS LEITURAS      ║
║                                  ║
║       1900 → Normal              ║
║       2048 → Normal              ║
║       3000 → Cheio               ║
║       800  → Vazio               ║
╚══════════════════════════════════╝
```
---
O projeto possui a possibilidade de ativar o **pré-processamento dos dados através de GPIO**, conforme especificado no projeto.

A filtragem deve ser compatível com a variável física monitorada, neste caso, o nível da água.

Quando utilizada, a filtragem tem como objetivo reduzir variações indesejadas nas leituras antes que os dados sejam utilizados pelas etapas seguintes do sistema.
---
O funcionamento do sistema ocorre da seguinte maneira:

1. O usuário altera a posição do **trimpot**;
2. O trimpot gera um valor analógico correspondente à posição configurada;
3. O ADC do STM32 converte esse sinal em um valor digital;
4. O STM32 envia a leitura pela **USB CDC / Porta COM**;
5. A aplicação C# recebe a leitura;
6. O C# organiza e trata o dado;
7. O valor é convertido para **JSON**;
8. O JSON é enviado para a **API REST**;
9. A API encaminha a leitura para o modelo de IA;
10. A IA classifica o nível como **Vazio, Normal ou Cheio**;
11. O resultado retorna para o sistema;
12. A interface Web atualiza o valor, a classificação e o histórico.

* STM32F103C8 (Blue Pill)
* Trimpot / Potenciômetro
* Conexão USB

* STM32CubeIDE
* HAL
* ADC
* USB CDC
* Comunicação serial

* C#
* Comunicação pela Porta COM
* JSON
* HTTP

* API REST
* HTTP
* JSON

* Machine Learning
* Modelo de classificação

* HTML
* CSS
* JavaScript

A organização do repositório deve separar os módulos do sistema:

📦 projeto-integrado
│
├── 📁 STM32
│   └── Código do microcontrolador
│
├── 📁 CSharp
│   └── Aplicação de comunicação
│
├── 📁 API
│   └── Servidor REST
│
├── 📁 IA
│   └── Modelo de classificação
│
├── 📁 Frontend
│   ├── index.html
│   ├── style.css
│   └── script.js
│
└── README.md
```
* Conecte o trimpot ao circuito;
* Configure o ADC no STM32;
* Compile e grave o programa no STM32F103C8;
* Conecte o STM32 ao computador;
* Verifique qual Porta COM foi atribuída ao dispositivo.

* Abra o projeto C#;
* Configure a Porta COM correspondente ao STM32;
* Execute a aplicação;
* Verifique se as leituras estão sendo recebidas continuamente.

* Inicie o servidor;
* Verifique se a API está disponível;
* Certifique-se de que a aplicação C# está utilizando o endereço correto da API.

* Inicie a aplicação Web;
* Acesse a página pelo navegador;
* Altere a posição do trimpot;
* Observe a atualização da leitura e da classificação.
---
Para testar o funcionamento, o trimpot pode ser colocado em diferentes posições.

Coloque o trimpot em uma posição correspondente à faixa de nível baixo.

**Resultado esperado:**

Leitura → valor baixo
Classificação → VAZIO
```

Coloque o trimpot em uma posição correspondente à faixa intermediária.

**Resultado esperado:**

Leitura → valor intermediário
Classificação → NORMAL
```

Coloque o trimpot em uma posição correspondente à faixa de nível alto.

**Resultado esperado:**

Leitura → valor alto
Classificação → CHEIO

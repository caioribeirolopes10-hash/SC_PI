const express = require('express');
const path = require('path');

const app = express();

app.use(express.json());

// HOME
app.get('/', (req, res) => {
    res.sendFile(path.join(__dirname, 'PI.html'));
});
app.use(express.json());

// DADOS ATUAIS DA ÁGUA
let dadosAgua = {nivel: 0,classificacao: "Aguardando", horario: null};
// HOME
app.get('/', (req, res) => {
    res.send(`<h1>Servidor do Monitoramento da Água</h1><p>Servidor funcionando normalmente.</p>`);
});

// LOGIN
app.post('/login', (req, res) => {
    const { email, senha } = req.body;
    console.log("Tentativa de login:", email);
    // Aqui depois podemos colocar
    // a validação real do usuário.

    if (email && senha) {
        return res.json({sucesso: true,mensagem: "Login recebido"});
    }
    res.status(400).json({sucesso: false,mensagem: "Email e senha são obrigatórios"});
});

// RECEBER RESULTADO DA IA
app.post('/api/agua', async (req, res) => {
    const { nivel } = req.body;

    if (nivel === undefined) {
        return res.status(400).json({ erro: "Nivel não informado" });
    }

    try {

        // CHAMAR A IA AUTOMATICAMENTE
        const respostaIA = await fetch('http://127.0.0.1:5000/prever', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify({
                nivel: Number(nivel)
            })
        });

        if (!respostaIA.ok) {
            throw new Error(`Erro na IA: ${respostaIA.status}`);
        }

        const resultadoIA = await respostaIA.json();

        console.log("Resposta da IA:");
        console.log(resultadoIA);

        // PEGAR CLASSIFICAÇÃO DA IA
        const classificacao = resultadoIA.classificacao;

        if (!classificacao) {
            return res.status(500).json({
                erro: "A IA não retornou uma classificação"
            });
        }

        // SALVAR DADOS
        dadosAgua = {
            nivel: Number(nivel),
            classificacao: classificacao,
            horario: new Date().toLocaleTimeString('pt-BR')
        };

        console.log("Novo resultado da IA:");
        console.log(dadosAgua);

        // RESPONDER
        res.json({
            sucesso: true,
            mensagem: "Dados recebidos e classificados pela IA",
            dados: dadosAgua
        });

    } catch (erro) {

        console.error("Erro ao chamar a IA:", erro.message);

        res.status(500).json({
            erro: "Não foi possível consultar a IA",
            detalhes: erro.message
        });
    }
});

// ENVIAR DADOS PARA O HTML
app.get('/api/agua', (req, res) => {
    res.json(dadosAgua);
});

// SERVIDOR
app.listen(3000, () => {
    console.log('');
    console.log('Servidor iniciado');
    console.log('Site: http://localhost:3000');
    console.log('API:  http://localhost:3000/api/agua');
    console.log('');
});
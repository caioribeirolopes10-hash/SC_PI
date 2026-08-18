const express = require('express');
const path = require('path');

const app = express();
const PORT = process.env.PORT || 3000;

app.use(express.json());

// Entrega a interface web pelo mesmo servidor da API.
app.get('/', (req, res) => {
    res.sendFile(path.join(__dirname, 'PI.html'));
});

// Último resultado recebido do C#.
let dadosAgua = {
    nivel: 0,
    classificacao: "Aguardando",
    horario: null
};

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

// Recebe do C# o nível e a classificação retornada pela IA.
app.post('/api/agua', (req, res) => {
    const { nivel, classificacao } = req.body;

    if (nivel === undefined || !classificacao) {
        return res.status(400).json({ erro: "Dados incompletos" });
    }

    const nivelNumerico = Number(nivel);

    if (!Number.isFinite(nivelNumerico)) {
        return res.status(400).json({ erro: "O nível precisa ser numérico" });
    }

    dadosAgua = {
        nivel: nivelNumerico,
        classificacao: String(classificacao),
        horario: new Date().toLocaleTimeString('pt-BR')
    };

    console.log("Novo resultado da IA:");
    console.log(dadosAgua);

    res.json({
        sucesso: true,
        mensagem: "Dados recebidos",
        dados: dadosAgua
    });
});
// Disponibiliza o último resultado para o HTML.
app.get('/api/agua', (req, res) => {
    res.json(dadosAgua);
});

app.listen(PORT, () => {
    console.log('');
    console.log('Servidor iniciado');
    console.log(`Site: http://localhost:${PORT}`);
    console.log(`API:  http://localhost:${PORT}/api/agua`);
    console.log('');
});
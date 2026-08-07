```javascript
const express = require('express');
const app = express();
app.use(express.json());

// DADOS ATUAIS DA ÁGUA
let dadosAgua = {nivel: 0,classificacao: "Aguardando" horario: null};
// HOME
app.get('/home', (req, res) => {
    res.send(`
        <h1>Servidor do Monitoramento da Água</h1>
        <p>Servidor funcionando normalmente.</p>
`);
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
    res.status(400).json({
        sucesso: false,
        mensagem: "Email e senha são obrigatórios"
    });
});

// RECEBER RESULTADO DA IA
app.post('/api/agua', (req, res) => {
    const { nivel, classificacao } = req.body;
    if (nivel === undefined || !classificacao) {
        return res.status(400).json({ erro: "Dados incompletos" });
    }
    dadosAgua = {nivel: Number(nivel),classificacao: classificacao, horario: new Date().toLocaleTimeString('pt-BR')};
    console.log("Novo resultado da IA:");
    console.log(dadosAgua);
    res.json({
        sucesso: true,
        mensagem: "Dados recebidos",
        dados: dadosAgua
    });
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
```

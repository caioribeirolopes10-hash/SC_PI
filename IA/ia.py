import os
import csv
import json

from flask import Flask, request, jsonify
from sklearn.neighbors import KNeighborsClassifier

DATASET_PATH = os.path.join(
    os.path.dirname(__file__),
    "dataset.csv"
)
N_NEIGHBORS = 5

app = Flask(__name__)


def load_dataset(path):
    X = []
    y = []

    with open(path, newline="", encoding="utf-8") as f:
        reader = csv.reader(f)
        next(reader)  # skip header

        for row in reader:
            *features, label = row
            X.append([float(v) for v in features])
            y.append(label)

    return X, y


@app.route("/prever", methods=["POST"])
def prever():

    try:
        dados = request.get_json()
        if dados is None:
            return jsonify({"error": "JSON não recebido"}), 400

        if "nivel" not in dados:
            return jsonify({"error": "Campo 'nivel' não encontrado"}), 400

        nivel = float(dados["nivel"])

        # Mantém o nível entre 0 e 100
        if nivel < 0:
            nivel = 0
        if nivel > 100:
            nivel = 100
        X, y = load_dataset(DATASET_PATH)

        # Verifica a quantidade de características
        if len([nivel]) != len(X[0]):
            return jsonify({"error": f"sample must have {len(X[0])} features"}), 400

        # Cria e treina o KNN
        knn = KNeighborsClassifier(n_neighbors=N_NEIGHBORS)
        knn.fit(X, y)

        # Faz a previsão
        prediction = knn.predict([[nivel]])[0]

        # Calcula as probabilidades
        proba = knn.predict_proba([[nivel]])[0]

        probabilities = {label: float(p)for label, p in zip(knn.classes_, proba)}

        # Retorna o resultado para o C#
        return jsonify({
            "nivel": nivel,
            "classificacao": prediction,
            "probabilidades": probabilities
        })
    except ValueError:

        return jsonify({"error": "O nível precisa ser numérico"}), 400

    except Exception as e:

        return jsonify({"error": str(e)}), 500


# Rota para verificar se a IA está funcionando
@app.route("/", methods=["GET"])
def inicio():

    return jsonify({
        "status": "online",
        "message": "IA de classificação do nível da água",
        "endpoint": "/prever"
    })


if __name__ == "__main__":
    app.run(host="127.0.0.1",port=5000,debug=True)

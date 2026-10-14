# Validação I04 — rascunhos

Validação local realizada com SDK .NET 10.0.402 em 14/10/2026:

- Build da solução: zero erros e zero warnings.
- MSTest: 19 testes aprovados, incluindo 11 novos testes de rascunhos.
- Verificação HTTP/SQLite suplementar: 25 requisições aprovadas.
- Histórico de criação/edição, valores anteriores/posteriores e renovação de
  Version conferidos no banco temporário.
- Edição de despesa de outro usuário: 404. Edição fora de Draft: 409, sem
  acrescentar histórico. Entradas protegidas, datas/valores/categorias inválidas:
  400. Rotas sem autenticação: 401. Perfil insuficiente: 403.
- Nenhum banco, token, senha ou arquivo de build deve ser incluído no commit.

As verificações HTTP utilizaram credenciais aleatórias somente em memória e banco
temporário. São evidência suplementar, não testes unitários pontuáveis. O script
temporário está em artifacts, ignorado pelo Git.

O SDK padrão desta máquina é .NET 9; para esta validação foi utilizado o executável
temporário `$env:TEMP\expensehub-dotnet10\dotnet.exe`. Em máquinas com .NET 10
instalado, os comandos padrão da documentação são suficientes.

Esta evidência não substitui o relatório do CI no SHA da PR. Aprovação, pagamento
e consulta de histórico continuam sob responsabilidade das próximas issues.

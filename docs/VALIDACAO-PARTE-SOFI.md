# Validação da parte I01–I03

Base consultada: Racass/checkpoint-csharpracass-expensehub, main,
SHA 58c8405387f5e9ae9725ad964917e7634a870b74.

## Resultados locais

- SDK utilizado: .NET 10.0.402. Projetos net10.0; pacotes Microsoft 10.0.12.
- I01 isolada: restore e build aprovados, zero erros e zero warnings.
- I02 aplicada sobre I01: restore e build aprovados, zero erros e zero warnings.
- I03 aplicada sobre I02: build aprovado, zero erros e zero warnings.
- MSTest: 8 aprovados, nenhum falhou ou foi ignorado.
- Testes HTTP/SQLite suplementares: 24 verificações aprovadas.
- Script oficial Invoke-CodeQuality.ps1, execução local: 100/100, status passed.

Comandos executados no ambiente de preparação:

```shell
dotnet build sources/ExpenseHub.slnx -m:1
dotnet test sources/ExpenseHub.slnx -m:1 --no-build
```

`-m:1` limita o paralelismo do MSBuild neste ambiente. Os comandos padrão do
professor continuam aplicáveis em um ambiente local com .NET 10 configurado.

## Verificações suplementares

API iniciada com SQLite temporário e credenciais aleatórias somente em ambiente.
Foram verificadas: saúde 200; rota administrativa anônima 401; login incorreto
401; login Admin; listagem de usuário inicial único; tentativa de cadastrar roles
400 sem criar conta; email/senha inválidos 400; cadastro 201; duplicidade 400;
login de usuário sem roles; bloqueio de listagem e alteração de roles por esse
usuário 403; bloqueio de auto-remoção Admin 400; role arbitrária 400; duplicatas
400; roles ausentes/nulas 400; usuário inexistente 404; atribuição Employee;
novo login; Employee sem acesso administrativo 403; remoção de roles; dados
persistidos e senha com hash; reinício sem duplicar usuários/roles e sem
necessitar novamente das credenciais de seed.

Os testes suplementares são de integração e não foram contados como testes
unitários nem como pontuação I09. Nenhuma senha ou token de teste foi incluído
neste pacote.

## Limites

Os endpoints de despesas I04–I08 e testes correspondentes I09 não foram
implementados. As entidades não substituem as regras de validação do serviço.
A suíte de oito testes cobre somente administração de roles.
O formulário não foi preenchido e nenhum push/PR externo foi realizado.
A execução local do script oficial produziu score 100/100. Isso não comprova
a execução do CI externo: I10 deve ser verificada no workflow do repositório do
grupo, no SHA final após as implementações dos colegas.

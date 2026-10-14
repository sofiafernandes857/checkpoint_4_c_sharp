# ExpenseHub — Checkpoint 2 de C#

API corporativa de reembolsos desenvolvida com ASP.NET Core, ASP.NET Core Identity, Entity Framework Core e SQLite.

O projeto implementa autenticação bearer, autorização por roles e proprietário, criação e edição de rascunhos, envio, aprovação, reprovação, pagamento simulado, histórico e testes unitários.

## Tecnologias

- .NET 10 e ASP.NET Core.
- ASP.NET Core Identity.
- Entity Framework Core.
- SQLite.
- MSTest.
- GitHub Actions e Gitleaks.

Pacotes Microsoft utilizados na API:
- Microsoft.AspNetCore.Identity.EntityFrameworkCore 10.0.12.
- Microsoft.EntityFrameworkCore.Sqlite 10.0.12.
- Microsoft.AspNetCore.OpenApi 10.0.12.

## Estrutura

```text
sources/
├── ExpenseHub.slnx
├── ExpenseHub.Api/
└── ExpenseHub.UnitTests/

docs/
scripts/
tests/
```

O projeto `ExpenseHub.Api` contém a API, as entidades, a persistência e as regras de negócio. O projeto `ExpenseHub.UnitTests` contém os testes unitários da aplicação.

Os diretórios `scripts` e `tests` na raiz contêm o pipeline oficial de qualidade e seus testes.

## Requisitos

- SDK .NET 10.
- Git.
- PowerShell para executar os exemplos abaixo.

Não é necessário instalar um servidor de banco de dados. O SQLite utiliza um arquivo local.

## Preparação

Na raiz do repositório, execute os comandos separadamente:

```powershell
dotnet restore ./sources/ExpenseHub.slnx
dotnet build ./sources/ExpenseHub.slnx
dotnet test ./sources/ExpenseHub.slnx
```

## Configuração do Admin

Antes da primeira inicialização, configure o email e a senha do Admin usando User Secrets.

Execute no mesmo terminal:

```powershell
$credenciais = Get-Credential -Message "Informe o email e a senha do Admin"

dotnet user-secrets set "AdminSeed:Email" ($credenciais.UserName) --project ./sources/ExpenseHub.Api

dotnet user-secrets set "AdminSeed:Password" ($credenciais.GetNetworkCredential().Password) --project ./sources/ExpenseHub.Api
```

Informe o email no campo de usuário da janela de credenciais.

A senha deve conter de 10 a 128 caracteres, incluindo maiúscula, minúscula, número e símbolo.

As credenciais ficam na configuração local do computador e não devem ser adicionadas ao Git.

A inicialização cria as roles `Admin`, `Employee`, `Approver`, `Finance` e `Auditor`, além de apenas uma conta Admin inicial. Depois que um Admin existe, reiniciar a aplicação não duplica a conta nem exige novamente as credenciais de seed.

Os demais usuários devem ser cadastrados pelo endpoint `POST /register`. O cadastro não atribui roles; o Admin deve concedê-las posteriormente.

## Iniciar a aplicação

Na raiz do repositório:

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"

dotnet run --project ./sources/ExpenseHub.Api --no-launch-profile --urls http://localhost:5050
```

O ambiente `Development` permite carregar os User Secrets configurados anteriormente.

Para verificar a inicialização, acesse:

```text
http://localhost:5050/health
```

O endpoint deve retornar HTTP 200.

Para encerrar a aplicação, pressione `Ctrl + C` no terminal em que ela está sendo executada.

## Banco de dados

Provider: SQLite.

Pacote: `Microsoft.EntityFrameworkCore.Sqlite`, versão `10.0.12`.

Contexto: `ExpenseHubDbContext`.

A configuração padrão é:

```text
Data Source=expensehub.db
```

O caminho é relativo ao diretório de trabalho da aplicação. Para utilizar outro arquivo local, configure a variável de ambiente antes de iniciar:

```powershell
$env:ConnectionStrings__ExpenseHub = "Data Source=C:/dados/expensehub.db"
```

A pasta escolhida deve existir e permitir escrita.

O banco é criado automaticamente por `EnsureCreatedAsync`. A inicialização também adiciona as categorias ausentes, sem criar usuários adicionais.

### Alterações de esquema

Esta versão não utiliza migrations. `EnsureCreatedAsync` cria o esquema inicial, mas não atualiza um banco existente após mudanças nas entidades.

Durante o desenvolvimento, um banco de teste descartável pode ser recriado após parar a API. Não remova um banco com dados que precisem ser preservados. Para evoluir o esquema preservando dados, será necessário adotar migrations e documentar a atualização.

Arquivos de banco local não devem ser versionados.

## Autenticação

`POST /login` recebe:

```json
{
  "email": "usuario@example.com",
  "password": "SENHA_DO_USUARIO"
}
```

A resposta utiliza o formato nativo do Identity, com `tokenType`, `accessToken`, `expiresIn` e `refreshToken`.

Nas rotas protegidas, envie:

```http
Authorization: Bearer <accessToken>
```

A credencial bearer é um token opaco protegido pelo ASP.NET Core Data Protection, não um JWT.

Esta versão não disponibiliza endpoint de renovação. Quando o token expirar, faça um novo login.

Após uma alteração de roles, o usuário precisa autenticar novamente para receber as novas permissões. Tokens de acesso já emitidos mantêm suas claims até expirarem; a atualização de roles não realiza revogação imediata desses tokens.

As chaves de Data Protection devem permanecer fora do Git.

## Perfis e permissões

| Role | Responsabilidade |
|---|---|
| `Admin` | Listar usuários e administrar roles |
| `Employee` | Criar, editar, enviar e consultar os próprios reembolsos |
| `Approver` | Consultar despesas enviadas e aprovar ou reprovar despesas não próprias |
| `Finance` | Consultar despesas aprovadas ou pagas e pagar despesas não próprias |
| `Auditor` | Consultar todas as despesas e seus históricos, sem permissão de escrita |

Roles podem ser combinadas, mas ninguém pode aprovar, reprovar ou pagar o próprio reembolso.

A role `Admin`, isoladamente, não concede acesso funcional às despesas. O Admin também não pode remover sua própria role `Admin`.

## Endpoints

| Método | Rota | Operação |
|---|---|---|
| GET | `/health` | Verificar inicialização |
| POST | `/register` | Cadastrar usuário sem roles |
| POST | `/login` | Autenticar usuário |
| GET | `/api/admin/users` | Listar usuários como Admin |
| PUT | `/api/admin/users/{id}/roles` | Substituir as roles de um usuário |
| GET | `/api/expense-categories` | Consultar categorias como Employee |
| POST | `/api/expenses` | Criar rascunho |
| PUT | `/api/expenses/{id}` | Editar rascunho próprio |
| GET | `/api/expenses` | Listar despesas conforme o perfil |
| GET | `/api/expenses/{id}` | Consultar uma despesa visível |
| POST | `/api/expenses/{id}/submit` | Enviar rascunho próprio |
| POST | `/api/expenses/{id}/approve` | Aprovar despesa enviada |
| POST | `/api/expenses/{id}/reject` | Reprovar com justificativa |
| POST | `/api/expenses/{id}/pay` | Registrar pagamento simulado |
| GET | `/api/expenses/{id}/history` | Consultar histórico autorizado |

Os contratos detalhados estão em [Despesas](docs/DESPESAS.md).

## Fluxo de reembolso

| Estado atual | Ação | Próximo estado |
|---|---|---|
| Inexistente | Criar | `Draft` |
| `Draft` | Editar | `Draft` |
| `Draft` | Enviar | `Submitted` |
| `Submitted` | Aprovar | `Approved` |
| `Submitted` | Reprovar | `Rejected` |
| `Approved` | Pagar | `Paid` |

`Rejected` e `Paid` são estados finais. Não existem reabertura, cancelamento, exclusão ou reenvio.

Transições repetidas ou incompatíveis retornam conflito e não devem duplicar o histórico.

## Validações e histórico

- Descrição obrigatória: 10 a 500 caracteres.
- Valor em `decimal`: de R$ 0,01 até R$ 2.147.483.647,00.
- Data da despesa válida e não futura, considerando a data atual em UTC.
- Categoria existente.
- Justificativa de reprovação obrigatória: 10 a 500 caracteres.
- Identificadores, proprietário, estados, atores e horários definidos pelo servidor.
- Edição e envio permitidos somente para rascunho próprio.
- Aprovação, reprovação e pagamento proibidos sobre despesa própria.
- Consultas filtradas conforme as roles antes da materialização dos dados.

O histórico registra criação, edição, envio, decisões e pagamento, incluindo ator, instante UTC, estados e informações da alteração.

A alteração da despesa e seus registros correspondentes são persistidos na mesma operação lógica. O pagamento simulado também registra um `PaymentRecord`.

## Respostas HTTP

| Status | Significado |
|---|---|
| 400 | Entrada inválida |
| 401 | Autenticação ausente ou inválida |
| 403 | Usuário autenticado sem permissão |
| 404 | Recurso inexistente ou fora do escopo de leitura |
| 409 | Estado incompatível, repetição de transição ou conflito de concorrência |

As respostas de erro utilizam `ProblemDetails` ou o formato de validação correspondente.

## Testes

Execute:

```powershell
dotnet test ./sources/ExpenseHub.slnx
```

Os testes unitários utilizam MSTest e verificam regras de roles, validações, ownership, visibilidade, estados, transições, decisões, pagamento e histórico.

Os testes unitários devem executar sem banco de dados, rede ou serviços externos.

As evidências registradas estão em:

- [Validação I07](docs/VALIDACAO-I07.md)
- [Validação I08](docs/VALIDACAO-I08.md)
- [Validação I09](docs/VALIDACAO-I09.md)

## Qualidade e integração contínua

O workflow `code-quality` executa verificações de compilação, segurança, higiene, estilo e boas práticas, além dos testes dos scripts de qualidade.

A execução final consultada apresentou:

- Pontuação: **100/100**.
- Status: **passed**.

Após novos commits, confira novamente a execução correspondente ao SHA final da `main`. Um resultado anterior não comprova a qualidade de alterações posteriores.

Os relatórios ficam disponíveis no GitHub Actions, no artefato `code-quality-report`.

Não remova nem enfraqueça analisadores, Gitleaks, configurações ou etapas do workflow para alterar a pontuação.

## Desenvolvimento em grupo

Para cada issue:

1. Atualizar a branch `main`.
2. Criar uma branch específica.
3. Implementar e validar as alterações.
4. Fazer commits pela própria conta.
5. Abrir uma pull request para `main`.
6. Referenciar a issue original, por exemplo `Racass/checkpoint-csharpracass-expensehub#6`.
7. Conferir os arquivos, o CI e o relatório de qualidade.
8. Integrar a PR preservando os commits dos integrantes.

Não utilize `Closes`, `Fixes` ou `Resolves` para referenciar as issues do professor.

Cada integrante deve possuir mais de um commit real no repositório final.

## Segurança

Não versione:

- Senhas e tokens.
- Arquivos de User Secrets.
- Connection strings sensíveis.
- Chaves de Data Protection.
- Bancos locais.
- Diretórios `bin` e `obj`.
- Artefatos locais de execução.

O uso de IA está documentado em [Uso de Inteligência Artificial](docs/USO-DE-IA.md). Cada integrante é responsável por revisar, compreender e validar o código de sua contribuição.

## Documentação

- [Enunciado](docs/ENUNCIADO.md)
- [Requisitos e contratos](docs/REQUISITOS.md)
- [Rubrica](docs/RUBRICA.md)
- [Matriz de autorização](docs/MATRIZ-AUTORIZACAO.md)
- [Processo no GitHub](docs/PROCESSO-GITHUB.md)
- [Uso de Inteligência Artificial](docs/USO-DE-IA.md)
- [Execução local](docs/EXECUCAO.md)
- [Despesas](docs/DESPESAS.md)
- [Divisão do grupo](docs/DIVISAO-GRUPO.md)
- [Regras de qualidade](docs/code-quality-rules.md)

## Entrega

Repositório público do grupo:

https://github.com/sofiafernandes857/checkpoint_4_c_sharp

Após integrar todas as alterações e confirmar o CI, obtenha o SHA final:

```powershell
git switch main
git pull --ff-only
git rev-parse HEAD
```

Entregue a URL pública, o SHA final e as demais informações solicitadas pelo professor. Confirme também o preenchimento do formulário dos integrantes.

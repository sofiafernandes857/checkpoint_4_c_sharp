# Executar a parte I01–I02

Requer SDK .NET 10. O template usa `net10.0` e pacotes 10.0.12.

```powershell
dotnet restore ./sources/ExpenseHub.slnx
dotnet build ./sources/ExpenseHub.slnx
dotnet test ./sources/ExpenseHub.slnx
# Configurar no computador local: substitua os valores, não salve senha no Git.
dotnet user-secrets set "AdminSeed:Email" "SEU_EMAIL_ADMIN" --project ./sources/ExpenseHub.Api
dotnet user-secrets set "AdminSeed:Password" "SUA_SENHA_FORTE_LOCAL" --project ./sources/ExpenseHub.Api
dotnet run --project ./sources/ExpenseHub.Api --no-launch-profile --urls http://localhost:5050
```

Provider: SQLite, pacote Microsoft.EntityFrameworkCore.Sqlite 10.0.12.
O contexto é ExpenseHubDbContext. As entidades são Expense, ExpenseCategory,
ExpenseHistory e PaymentRecord. O campo Amount usa decimal; a validação funcional
será implementada em I04. O índice único de PaymentRecord.ExpenseId impede
pagamentos duplicados. Version é um token de concorrência que o serviço deve
renovar a cada alteração, tratando DbUpdateConcurrencyException como 409.

O banco é criado automaticamente ao iniciar, por EnsureCreatedAsync, no diretório
de trabalho, com `Data Source=expensehub.db`. Pode-se substituir o caminho por
`ConnectionStrings__ExpenseHub` no ambiente, sem versionar configurações sensíveis.

EnsureCreated cria um banco novo, mas não atualiza o esquema de um banco existente.
Esta entrega inicial não usa migrations. Antes de introduzir mudanças no esquema
com dados reais, substituir por EF migrations e documentar a migração. Durante a
montagem do checkpoint, somente para banco de teste descartável, parar a API e
remover os arquivos locais expensehub.db, expensehub.db-shm e expensehub.db-wal
para recriar o esquema. Não apagar um banco com dados que devam ser preservados.

`GET http://localhost:5050/health` deve retornar 200.
Identity usa Microsoft.AspNetCore.Identity.EntityFrameworkCore 10.0.12 com SQLite.
As roles Admin, Employee, Approver, Finance e Auditor são criadas pelo seed.
Apenas a conta Admin inicial é criada. Sem configuração segura no primeiro início,
a API interrompe a inicialização. Depois que um Admin existe, reiniciar não cria
outro usuário nem exige novamente a senha. Não existe senha padrão no código.

POST /login recebe {"email":"...","password":"..."} e retorna o formato nativo
Identity: tokenType, accessToken, expiresIn, refreshToken. O bearer é um token
opaco protegido pelo ASP.NET Core Data Protection, não um JWT. Use
Authorization: Bearer <accessToken> nas rotas protegidas. Nenhuma chave JWT é
necessária. Esta etapa não oferece endpoint de refresh; faça um novo login ao expirar.
As chaves Data Protection devem permanecer locais e fora do Git; não apague-as
se precisar preservar a validade de tokens já emitidos.

I02 expõe apenas login e health; as rotas protegidas chegam em I03. Endpoints de
despesas ainda serão criados pelos outros integrantes.
Se executou I01 antes de instalar I02, recrie somente o banco de teste descartável,
pois o esquema agora inclui as tabelas de Identity. O seed é executado em uma
transação para evitar conta Admin criada parcialmente.

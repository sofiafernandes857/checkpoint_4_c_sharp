# Executar a parte I01

Requer SDK .NET 10. O template usa `net10.0` e pacotes 10.0.12.

```powershell
dotnet restore ./sources/ExpenseHub.slnx
dotnet build ./sources/ExpenseHub.slnx
dotnet test ./sources/ExpenseHub.slnx
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
Esta etapa ainda não implementa Identity nem endpoints de despesas.

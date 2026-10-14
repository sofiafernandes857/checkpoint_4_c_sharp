# I08 — Pagamento e histórico

Referência: https://github.com/Racass/checkpoint-csharpracass-expensehub/issues/8

- POST /api/expenses/{id}/pay, sem corpo: Finance não proprietário, Approved → Paid.
- GET /api/expenses/{id}/history: mesma união de visibilidade de lista e detalhe.
- O servidor gera identificadores, ator autenticado e horário UTC.
- Pagamento, estado e histórico são persistidos em um único SaveChanges relacional.
- Version original participa do UPDATE; concorrência ou pagamento duplicado retorna 409.
- O índice único de PaymentRecord.ExpenseId permanece como proteção adicional.
- Uma falha descarta o tracking; a transação do EF impede gravação parcial.

A interface IExpenseTransitionStore permite testar o serviço com snapshots e
persistência em memória, sem banco. O adaptador EF continua usando o mesmo contexto
e as entidades existentes. A decisão recebe um snapshot isolado; o commit compara
a versão original. Aprovar/reprovar também usam essa fronteira atômica.

O histórico é filtrado no SQL antes da materialização. A ordenação por instante
ocorre depois, pois SQLite não ordena DateTimeOffset diretamente. Não há escrita
disponível para Auditor sem outra role funcional.

Validação: build sem warnings e testes ExpensePaymentTests, além das suítes
anteriores. A I09 acrescenta testes do serviço para repetição, falha e concorrência.
CI e score remoto continuam pendentes até publicação das branches.

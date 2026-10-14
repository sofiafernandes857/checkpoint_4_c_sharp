# Validação I06 — ownership e matriz

Validação local realizada com SDK .NET 10.0.402 em 14/10/2026:

- Build: zero erros e zero warnings.
- MSTest: 31 testes aprovados, sem ignorados. Foram adicionados sete métodos de
  testes de acesso, incluindo todas as 32 combinações das cinco roles contra os
  cinco estados e despesas próprias/de terceiros.
- Autoaprovação/reprovação/autopagamento: bloqueados pelas regras reutilizáveis
  com qualquer combinação de roles. Essas regras não substituem os endpoints
  de I07/I08, ainda não implementados.
- Verificação HTTP/SQLite suplementar: 90 requisições aprovadas.
- dotnet format --verify-no-changes --no-restore: aprovado.
- Script oficial Invoke-CodeQuality.ps1 com PowerShell 7, .NET 10 e Gitleaks:
  score local 100/100, status passed. Relatórios em artifacts/code-quality,
  ignorados pelo Git. Nenhuma configuração oficial foi enfraquecida.

O filtro Visibility foi aplicado na consulta ao banco e os detalhes fora do
escopo retornaram 404. As regras de edição/envio agora compartilham o bloqueio de
ownership/estado. RequireDecision e RequirePayment estão disponíveis para o
colega da I07/I08; consulte DESPESAS.md para os contratos de integração.

O relatório local inclui as alterações da árvore de trabalho da I06 e não prova
execução do CI externo. O GitHub recusou a autenticação no push da I04. As três
branches permanecem locais, com integração pendente em ordem I04 → I05 → I06.
Textos propostos e passos de publicação estão em PRS-I04-I06.md.

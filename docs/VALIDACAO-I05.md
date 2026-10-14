# Validação I05 — envio e consultas

Validação local realizada com SDK .NET 10.0.402 em 14/10/2026:

- Build: zero erros e zero warnings.
- MSTest: 24 testes aprovados; cinco novos casos de envio.
- Verificação HTTP/SQLite suplementar: 90 requisições aprovadas, incluindo a I04.
- Envio de Draft próprio: 200, versão renovada e histórico Draft → Submitted.
- Reenvio: 409; edição após envio: 409; ambos sem histórico extra.
- Envio de despesa de terceiro: 404; envio por Approver sem Employee: 403.
- Listas e detalhes conferidos para Employee, Approver, Finance, Auditor e a
  combinação Approver/Finance, com registros de todos os estados.
- Admin sozinho/usuário sem roles: 403. Leitura anônima: 401.
- Detalhe invisível/inexistente: 404, sem revelar a existência da despesa.

Os estados Approved/Rejected/Paid foram preparados diretamente no SQLite
temporário para verificar consultas; isso não comprova implementação de aprovação,
reprovação ou pagamento, que pertencem à I07/I08. As verificações HTTP não são
testes unitários pontuáveis. Nenhuma credencial ou banco foi incluído nos fontes.

O push da I04 falhou por autenticação do GitHub. As branches locais são sequenciais:
I05 depende de I04. Publicar e integrar na ordem I04 → I05 → I06, após configurar
acesso ao repositório. Esta validação não substitui o CI externo no SHA da PR.

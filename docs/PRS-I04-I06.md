# Publicar e revisar I04–I06

As branches locais são encadeadas: I05 contém I04 e I06 contém ambas. A main local
foi preservada. O primeiro push falhou por autenticação GitHub. Antes de publicar,
confira acesso de colaborador, autentique o Git e confirme nome/e-mail de autoria.
Não inclua tokens nos comandos ou arquivos do projeto.

Publique uma branch por vez, revise o diff, confira o relatório de qualidade do CI
e integre na ordem abaixo. Os textos são propostas de descrição: conclua a revisão
antes de declarar itens como verificados.

## I04

```powershell
git push -u origin i04-expense-draft
```

Título: `I04 — Criar e editar rascunhos próprios`

Descrição sugerida:

> Referência: Racass/checkpoint-csharpracass-expensehub#4.
>
> Implementa criação e edição de rascunhos via ExpenseService. O proprietário vem
> da identidade autenticada; somente Employee pode criar, e somente o proprietário
> pode editar Draft. DTO aceita apenas campos editáveis e valida descrição,
> valor, data e categoria. Despesa e histórico são gravados juntos, com instantes
> UTC e renovação de Version na edição. Inclui seed de categorias e rota de consulta.
>
> Validação local: build sem erros/warnings, 19 testes unitários aprovados e
> 25 requisições HTTP/SQLite suplementares. Ver docs/VALIDACAO-I04.md e DESPESAS.md.

## I05

Após integrar I04, publique I05 e abra a PR para main. O diff deve mostrar somente
o incremento da I05; se I04 foi integrada por squash, reconcilie a branch antes da
PR para evitar reapresentar as mesmas alterações.

```powershell
git push -u origin i05-submit-query
```

Título: `I05 — Enviar e consultar despesas visíveis`

Descrição sugerida:

> Referência: Racass/checkpoint-csharpracass-expensehub#5.
>
> Implementa envio de Draft próprio para Submitted, com histórico e versão na
> mesma operação. Reenvio retorna 409 sem histórico duplicado. Listagem e detalhe
> aplicam a união da visibilidade por role no IQueryable, antes da materialização;
> detalhes invisíveis retornam 404 e Admin sozinho não tem acesso funcional.
>
> Validação local: build sem erros/warnings, 24 testes unitários aprovados e
> 90 requisições HTTP/SQLite suplementares. Ver docs/VALIDACAO-I05.md.

## I06

Após integrar I05, publique I06 com o mesmo cuidado quanto ao diff/squash.

```powershell
git push -u origin i06-ownership-access
```

Título: `I06 — Consolidar ownership e matriz de acesso`

Descrição sugerida:

> Referência: Racass/checkpoint-csharpracass-expensehub#6.
>
> Centraliza regras de ownership e estado usadas por edição/envio e oferece
> RequireDecision/RequirePayment para integração da I07/I08. As regras exigem a
> role correspondente, impedem ações sobre despesa própria e validam o estado.
> Testa as 32 combinações de roles para visibilidade e preserva os bloqueios de
> autoaprovação/autopagamento mesmo com permissões acumuladas.
>
> Validação local: build sem erros/warnings, 31 testes unitários aprovados e
> 90 requisições HTTP/SQLite suplementares. Ver docs/VALIDACAO-I06.md.

## Checklist de revisão para cada PR

- Conferir critérios de aceite e casos negativos da issue.
- Revisar os diffs e conseguir explicar regras e testes.
- Executar build e testes no SHA publicado.
- Conferir o score do artefato code-quality-report; CI verde não significa 100.
- Confirmar documentação atualizada e ausência de banco, bin/obj ou credenciais.
- Após integrar I06, informar ao colega de I07/I08 os contratos de DESPESAS.md.

Não usar Closes/Fixes/Resolves nas referências ao backlog central. A main só deve
ser atualizada pelo fluxo de PR do grupo após a revisão e o CI.

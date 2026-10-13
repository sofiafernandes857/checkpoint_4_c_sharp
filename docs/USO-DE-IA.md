# Uso de Inteligência Artificial

O uso de Inteligência Artificial (IA) é permitido. O objetivo não é evitar ferramentas, mas demonstrar engenharia, validação e autoria.

## Regras

- Não inclua credenciais, dados pessoais ou conteúdo confidencial em prompts.
- Revise toda sugestão antes de incorporá-la.
- Valide comportamento, autorização, erros e testes.
- Mantenha commits que mostrem evolução e correções.
- Você é responsável por todo código entregue, inclusive código gerado.

## Evidência de autoria

A avaliação poderá considerar em conjunto:

- histórico de commits;
- branches e pull requests;
- coerência entre decisões e implementação;
- testes que detectam falhas reais;
- ausência de grandes blocos inseridos sem revisão;
- capacidade do repositório de explicar como executar e validar o sistema.

Além da exigência de mais de um commit por integrante, não existe uma quantidade
ideal de commits. O histórico precisa ser plausível e tecnicamente coerente.

## Uso inadequado

São exemplos:

- entregar código que o aluno não revisou;
- inventar validações;
- manter código inseguro apenas porque foi sugerido pela ferramenta;
- inserir segredos em prompts ou no repositório;
- falsificar autoria ou histórico.

## Registro inicial do grupo — parte I01–I03

A implementação inicial de fundação, Identity e administração de roles foi
preparada com auxílio do Codex. O pacote contém patches sem autor/committer e
não contém commits produzidos pela ferramenta. Sofi deve revisar o código,
reproduzir a validação e registrar as alterações pela própria conta.
O código dos demais integrantes ainda será implementado e revisado por eles.

Decisões: SQLite relacional, Identity bearer nativo (token opaco), senha Admin
via configuração segura, cadastro sem roles e troca de roles transacional.
Limites e evidências estão em VALIDACAO-PARTE-SOFI.md; responsabilidades dos
colegas estão em DIVISAO-GRUPO.md.

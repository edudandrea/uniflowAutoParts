# Margem geral do estoque

A tela Estoque oferece uma única configuração **Margem de preço do estoque**, por empresa. Defina a porcentagem e clique em **Aplicar a todo o estoque**. A confirmação informa que todos os produtos serão recalculados, inclusive inativos, sem saldo, não selecionados ou ocultos pelos filtros.

O cálculo mantém a regra existente: preço de venda = custo × (1 + margem / 100), arredondado para centavos. Por exemplo, custo de R$ 100,00 e margem de 30% resultam em R$ 130,00. Zero é válido e iguala preço e custo. São aceitas até quatro casas decimais no percentual.

A configuração e os preços são gravados juntos. A mesma margem é aplicada automaticamente nos novos cadastros e nas alterações de custo/preço, incluindo importações. Quantidades e documentos fiscais já registrados não são alterados. O catálogo de vendas recebe os preços persistidos.

A migração GlobalStockPriceMargin adiciona Empresas.MargemPrecoEstoque, inicialmente sem valor: os preços atuais permanecem até a primeira aplicação explícita. A API aplica a migração na inicialização. Publicar backend e frontend para ativar a configuração.

GET/PUT /api/estoque/margem leem e atualizam a configuração da empresa autenticada. A rota antiga por produto responde com conflito e orientação para usar a margem geral. Os testes usam bancos descartáveis e APIs simuladas, sem alterar o estoque de produção.

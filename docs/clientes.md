# Consulta e edicao de clientes

A pesquisa continua na pagina Clientes. Clicar no cadastro abre um modal com os dados reais: identificacao, inscricoes, condicao fiscal, endereco, contatos, informacoes comerciais e observacoes. A antiga area de detalhes em linha foi removida.

O botao Editar cadastro abre um formulario preenchido. Salvar atualiza o mesmo cliente por PUT /api/clientes/{id}, preservando seu ID, empresa e data de criacao. Cancelar descarta as alteracoes do formulario. A API verifica empresa autenticada, documento duplicado, tipo de pessoa, indicador de IE e endereco da mesma empresa.

Pessoas juridicas possuem campos de Inscricao Estadual e Municipal; o cliente contribuinte precisa informar IE. O indicador de IE e o campo consumidor final sao editaveis e utilizados na consulta fiscal do destinatario. Pessoas fisicas continuam sendo cadastradas com CPF. Nao e necessaria migracao de banco, pois esses campos ja existem.

A consulta GET /api/clientes/{id} carrega o cadastro completo. A edicao preserva contatos adicionais e o endereco existente; a aba Enderecos permite selecionar outro endereco cadastrado na empresa. A manutencao dos enderecos compartilhados continua no menu Configuracoes.

Validacao: testes de edicao e isolamento de empresas em CustomerEditorChecks, testes HTTP de GET/PUT, testes Angular do formulario e CustomerEditorBrowser com API simulada. Para disponibilizar, publique backend e frontend atualizados.

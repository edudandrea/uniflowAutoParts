# Schemas NF-e 4.00

Fonte: SEFAZ/SVRS, pacote oficial `PL_010c_NT2022_002v1.30.zip`, publicado no portal de documentos NF-e.

- Documentos: https://dfe-portal.svrs.rs.gov.br/Nfe/Documentos
- Pacote: https://dfe-portal.svrs.rs.gov.br/NFE/DownloadArquivoEstatico/?sistema=NFE&tipoArquivo=2&nomeArquivo=PL_010c_NT2022_002v1.30.zip
- Os cinco arquivos XSD foram extraídos sem alterações. A geração usa os grupos de NF-e 55 suportados e valida o XML assinado localmente antes de reservar o número e transmitir.

A validação XSD não substitui as regras de negócio e a autorização do serviço da SEFAZ. A operação só passa para `Autorizada` com retorno, chave, ambiente, digest e protocolo correspondentes ao XML transmitido.
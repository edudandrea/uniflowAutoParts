# Confianca TLS da SEFAZ/RS

Este diretorio contem somente um certificado publico de autoridade raiz. Nao contem certificado A1, chave privada ou senha de empresa.

## Origem e identificacao

- Autoridade: Autoridade Certificadora Raiz Brasileira v10 (ICP-Brasil, SSL).
- Repositorio do ITI: https://www.gov.br/iti/pt-br/assuntos/repositorio/repositorio-ac-raiz
- Download oficial: https://acraiz.icpbrasil.gov.br/credenciadas/RAIZ/ICP-Brasilv10.crt
- Copia oficial conferida: https://repositorio.serpro.gov.br/docs/icpbrasilv10.crt
- Cadeia SERPRO: https://certificados.serpro.gov.br/serprossl/ifr-certificate-chain
- SHA-256 do certificado DER: 6E:0B:FF:06:9A:26:99:4C:15:DE:2C:48:88:CC:54:AF:84:88:2E:54:95:B7:FB:F6:6B:E9:CC:FF:EC:74:89:F6.
- Validade: 01/07/2019 a 01/07/2032 (UTC).
- Obtido e comparado entre ITI e SERPRO em 13/11/2026. As duas fontes forneceram o mesmo certificado.

## Motivo

Em 13/11/2026, nfe.sefazrs.rs.gov.br apresentou um certificado para *.sefazrs.rs.gov.br, emitido pela Autoridade Certificadora do SERPRO SSLv1. O servidor forneceu a intermediaria, mas o container da API nao confiava na raiz ICP-Brasil v10. O resultado era SecureConnectionError / PartialChain antes de qualquer resposta HTTP, tanto na emissao quanto na consulta.

O Dockerfile instala a raiz com update-ca-certificates. A impressao digital DER e a assinatura propria sao verificadas no build. Nao sao instalados certificados finais ou intermediarios como raizes e nao sao usados callbacks que ignorem erros TLS. A validacao normal do hostname, cadeia e validade permanece ativa. A raiz e incluida na imagem, sem depender de download de certificados durante a inicializacao.

Para ativar, reconstrua e publique o servico backend com back/Dockerfile. Apenas reiniciar uma imagem antiga nao instala a raiz. Se o servico usar outro builder, aplique a mesma instalacao de certificados no runtime efetivamente utilizado.

Quando a SEFAZ alterar a cadeia, obtenha os certificados apenas de fontes oficiais, confira a nova cadeia e atualize o arquivo e a impressao digital juntos. Nao substitua esta raiz pelo certificado do site nem pelo A1 da empresa.

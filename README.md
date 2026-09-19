# MediaTools.Api

Serviço interno do HAL9000 para disponibilizar operações relacionadas a mídia através de HTTP.

## Estado atual

Sprint 5 — Dockerização e preparação para deploy.

## Requisitos

- .NET SDK 9
- Docker Engine e Docker Compose para executar a imagem;
- `ffprobe` disponível no `PATH` ou configurado explicitamente
- `ffmpeg` disponível no `PATH` ou configurado explicitamente

## Configuração

```json
{
  "MediaTools": {
    "MediaRoot": "/media",
    "FfprobePath": "ffprobe",
    "FfmpegPath": "ffmpeg",
    "ProbeTimeoutSeconds": 30,
    "ExtractTimeoutSeconds": 120
  }
}
```

Os valores podem ser sobrescritos por configuração do ASP.NET Core. Exemplo:

```bash
MediaTools__MediaRoot=/caminho/local \
MediaTools__FfprobePath=/usr/bin/ffprobe \
MediaTools__FfmpegPath=/usr/bin/ffmpeg \
dotnet run --project src/MediaTools.Api
```

## Como executar

```bash
dotnet run --project src/MediaTools.Api
```

## Docker

A imagem usa um build multi-stage com .NET 9, `ffmpeg` e `ffprobe`. O runtime
final não contém o SDK e executa como o usuário não-root `app` (UID 1654).

Construção:

```bash
docker build --pull -t media-tools-api:local .
```

Diagnóstico das ferramentas incluídas:

```bash
docker run --rm --entrypoint ffmpeg media-tools-api:local -version
docker run --rm --entrypoint ffprobe media-tools-api:local -version
docker run --rm --entrypoint id media-tools-api:local
```

O container escuta em `8080` e possui healthcheck em `/health`. Para usar o
Compose, copie `.env.example` para `.env`, ajuste `MEDIA_ROOT_HOST` e garanta
que o host permita ao UID 1654 ler vídeos e criar arquivos `.srt`:

```bash
cp .env.example .env
docker compose config
docker compose up --build -d
curl http://127.0.0.1:5050/health
docker compose ps
docker image ls media-tools-api:local
```

O volume do host é montado em `/media`. A publicação local é limitada a
`127.0.0.1:5050`; a porta interna do container é `8080`. Não são necessários
Docker socket, `privileged`, capabilities extras ou permissões `777`.

Para uma validação reproduzível sem mídia real, gere um vídeo mínimo em um
diretório temporário com o `ffmpeg` da imagem, monte-o em `/media` e invoque
`POST /api/media/probe`. Um vídeo sem legendas deve retornar HTTP 200 com
`subtitles: []`.

A rede `media-tools-network` fica preparada para uma futura composição com o
n8n. Quando essa integração for implementada, o n8n deverá chamar
`http://media-tools-api:8080`; IP fixo e `host.docker.internal` não fazem parte
da arquitetura.

## Como testar

```bash
dotnet restore
dotnet build --no-restore
dotnet test --no-build --no-restore
dotnet format --verify-no-changes
```

O serviço valida a configuração no startup. `MediaRoot` deve ser absoluto;
`FfprobePath`, `FfmpegPath` e os timeouts devem ser válidos. Uma raiz que ainda
não exista gera apenas um warning e não impede o startup. Os corpos dos dois
endpoints POST são limitados a 16 KiB; não há upload multipart.

## Endpoints atuais

### Health

```http
GET /health
```

Resposta esperada:

```json
{
  "status": "healthy"
}
```

### Probe de mídia

```http
POST /api/media/probe
Content-Type: application/json
```

Request:

```json
{
  "path": "/media/example.mkv"
}
```

Resposta:

```json
{
  "path": "/media/example.mkv",
  "subtitles": [
    {
      "index": 2,
      "codec": "ass",
      "language": "por",
      "title": "Portuguese (Brazil)"
    }
  ]
}
```

Arquivos sem faixas de legenda retornam HTTP 200 com `subtitles: []`.

Códigos de erro relevantes:

- `400 invalid_path`: caminho inválido, relativo, fora da raiz, diretório ou link simbólico;
- `404 file_not_found`: arquivo inexistente dentro da raiz permitida;
- `422 ffprobe_failed`: o ffprobe não conseguiu inspecionar o arquivo;
- `503 ffprobe_unavailable`: o executável não pôde ser iniciado;
- `504 probe_timeout`: o tempo limite da inspeção foi excedido.
- `400 invalid_request`: JSON ausente, inválido ou incompatível com o contrato;
- `413 request_too_large`: corpo acima de 16 KiB;
- `415 unsupported_media_type`: Content-Type diferente de `application/json`;
- `500 internal_error`: falha inesperada, sem detalhes técnicos na resposta.

### Extração de legenda

```http
POST /api/media/extract-subtitle
Content-Type: application/json
```

Request:

```json
{
  "path": "/media/Planetes/episodio.mkv",
  "streamIndex": 3,
  "targetLanguage": "pt-BR"
}
```

Resposta HTTP 201:

```json
{
  "sourcePath": "/media/Planetes/episodio.mkv",
  "streamIndex": 3,
  "codec": "ass",
  "language": "por",
  "targetLanguage": "pt-BR",
  "outputPath": "/media/Planetes/episodio.pt-BR.srt",
  "writtenBytes": 63488
}
```

Codecs textuais permitidos:

- `subrip`
- `ass`
- `ssa`
- `webvtt`
- `mov_text`

O nome de saída é derivado internamente como
`<nome-base>.<targetLanguage>.srt`, sempre no diretório da mídia. A extração
escreve primeiro em um arquivo temporário no mesmo filesystem e só então move
o resultado para o nome definitivo. Arquivos existentes nunca são
sobrescritos.

Códigos de erro relevantes:

- `400 invalid_path`, `invalid_stream_index` ou `invalid_target_language`;
- `404 file_not_found` ou `subtitle_stream_not_found`;
- `409 subtitle_already_exists`;
- `422 unsupported_subtitle_codec`, `ffmpeg_failed` ou `ffprobe_failed`;
- `503 ffmpeg_unavailable` ou `ffprobe_unavailable`;
- `504 extract_timeout` ou `probe_timeout`.

Todos os erros controlados usam o formato `{ "error": "...", "message": "..." }`.
Cancelamentos do cliente encerram o processo filho e não são convertidos em
erro 500. Extrações concorrentes para o mesmo destino são serializadas: uma
requisição conclui com 201 e as demais recebem 409 sem sobrescrever o arquivo.

Exemplo:

```bash
curl -X POST http://127.0.0.1:5080/api/media/extract-subtitle \
  -H "Content-Type: application/json" \
  -d '{
    "path": "/media/Planetes/episodio.mkv",
    "streamIndex": 3,
    "targetLanguage": "pt-BR"
  }'
```

## Roadmap resumido

- Sprint 2 — ffprobe / inspeção de legendas
- Sprint 3 — extração com ffmpeg
- Sprint 4 — hardening, erros e robustez operacional
- Sprint 5 — Dockerização e preparação para deploy (atual)
- Sprint 6 — integração com n8n
- Sprint 7 — smoke tests finais

Os smoke tests reais com os arquivos Planetes e Murder Club permanecem
pendentes para a Sprint 7 e até o deploy no HAL9000.

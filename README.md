# gRPConnect

O **gRPConnect** é uma aplicação de chat em tempo real, desenvolvida com foco em alta performance e comunicação bidirecional utilizando o ecossistema gRPC e .NET 10.0.

---

## Arquitetura do Projeto

O projeto segue uma estrutura modular para garantir a separação de responsabilidades:

```
ChatGrpc.sln

src/
│
├── Chat.Server/       → Servidor gRPC: conexões, salas, roteamento de mensagens
├── Chat.Client/       → Aplicação console: interface e interação com o usuário
├── Chat.Contracts/    → Contratos (chat.proto) que definem a comunicação
└── Chat.Shared/       → Modelos e lógica compartilhados entre os componentes
```

| Projeto | Responsabilidade |
|---|---|
| `Chat.Client` | Aplicação cliente responsável pela interface e interação com o usuário, consumindo o serviço gRPC. |
| `Chat.Contracts` | Contém os arquivos `.proto` (Protocol Buffers) que definem o contrato de comunicação entre cliente e servidor. |
| `Chat.Server` | Servidor gRPC que gerencia conexões, salas de chat e o roteamento de mensagens. |
| `Chat.Shared` | Modelos e lógica compartilhados entre os componentes. |

---

## Tecnologias Utilizadas

- **.NET 10.0** — Framework principal para o desenvolvimento da aplicação.
- **gRPC** — Framework de RPC de alto desempenho, utilizado para a comunicação eficiente entre cliente e servidor.
- **Protocol Buffers (Protobuf)** — Mecanismo de serialização de dados estruturados para os contratos de interface.

---

## Técnicas e Conceitos Aplicados

- **Bidirectional Streaming** — Utilização de streams gRPC para permitir que cliente e servidor enviem mensagens simultaneamente de forma contínua.
- **Gestão de Conexões** — Uso de `ConcurrentDictionary` no servidor para gerenciar as conexões dos clientes de forma thread-safe.
- **Gerenciamento de Assincronia** — Implementação robusta de `async/await` e manipulação de fluxos assíncronos (`IAsyncEnumerable`).
- **CancellationToken** — Implementação estratégica para o cancelamento gracioso de operações, garantindo que conexões sejam encerradas corretamente e evitando vazamentos de recursos.
- **Injeção de Dependência** — Utilizada no servidor para gerenciar os serviços e dependências da aplicação.

---

## Como Executar

### Requisitos

- .NET SDK 10.0 ou superior.

### Comandos

Na raiz do repositório, execute:

**1. Iniciar o servidor:**

```bash
dotnet run --project src/Chat.Server --launch-profile https
```

**2. Iniciar o cliente (em outro terminal):**

```bash
dotnet run --project src/Chat.Client --launch-profile https
```

> A flag `--launch-profile https` força o uso do profile HTTPS configurado no `launchSettings.json`, garantindo que o canal gRPC do cliente se conecte corretamente ao endpoint esperado pelo servidor.

---

## Roteiro de Desenvolvimento

O projeto foi construído em etapas incrementais, cada uma isolando um conceito específico de gRPC e C# antes de avançar para o próximo nível de complexidade.

### Etapa 1 — Contrato gRPC
**Objetivo:** definir o `chat.proto` e gerar código.

- `syntax`, `package`, mensagens `User`, `ChatMessage`, `Room`
- `service ChatService` com o RPC `Connect` (bidirectional streaming)
- Configuração de geração automática no `.csproj` (`<Protobuf Include="..." />`)
- Confirmação de que as classes geradas aparecem em `obj/Debug/.../Generated`

**Concluída quando:** foi possível instanciar `ChatMessage` tanto no Server quanto no Client.

### Etapa 2 — Conexão simples (versão ingênua)
**Objetivo:** cliente conecta, servidor sabe que ele existe.

- Implementação de `ChatService.Connect` no server, mantendo uma lista simples (`List<ClientConnection>` com `lock`) — de propósito, ainda sem `ConcurrentDictionary`
- Client conecta via `GrpcChannel`, envia nome e sala
- Log simples no console do server (`"Fulano conectou"`)

**Concluída quando:** foi possível conectar 2+ clientes ao mesmo tempo e ver ambos logados no server.

> 💡 Ponto de aprendizado: iterar a lista enquanto outro thread adiciona um cliente gera problema de concorrência — o gancho natural para a próxima etapa.

### Etapa 3 — Concorrência de verdade
**Objetivo:** trocar a lista ingênua por `ConcurrentDictionary`, usar `CancellationToken` corretamente.

- Migração para `ConcurrentDictionary<Guid, ClientConnection>`
- Cada conexão usa `Guid.NewGuid()` como chave
- Propagação do `CancellationToken` do `ServerCallContext` para o loop de leitura/escrita

**Concluída quando:** múltiplos clientes conectando/desconectando rapidamente não geram exceptions nem estado inconsistente.

### Etapa 4 — Envio e broadcast de mensagens
**Objetivo:** mensagens fluem cliente → servidor → todos.

- `await foreach` no `IAsyncStreamReader` do server para ler mensagens do cliente
- Loop de broadcast usando `IServerStreamWriter` para cada conexão ativa
- Cliente exibe mensagens recebidas em uma task separada (leitura assíncrona em paralelo ao envio)

**Concluída quando:** com 3 clientes conectados, a mensagem de um aparece nos outros dois em tempo real.

### Etapa 5 — Salas
**Objetivo:** segmentar broadcast por sala.

- Adição de `Room` no fluxo de conexão
- Broadcast filtrado com LINQ: `_clients.Values.Where(c => c.Room == msg.Room)`
- Cliente escolhe a sala ao entrar

**Concluída quando:** mensagem enviada em "Geral" não aparece para quem está em "Games".

### Etapa 6 — Lista de usuários online
**Objetivo:** comando `/users`.

- Server responde com a lista da sala atual usando `Select()`, `OrderBy()`, `Count()`
- Client trata comandos que começam com `/` antes de tratar como mensagem normal

**Concluída quando:** `/users` mostra a lista correta e atualizada da sala.

> Nota: a ordem original do plano foi invertida — lista de usuários antes de mensagem privada — porque faz mais sentido saber quem está online antes de mandar uma DM.

### Etapa 7 — Mensagens privadas
**Objetivo:** comando `/private <nome> <mensagem>`.

- Server usa `FirstOrDefault()` para localizar o destinatário
- Tratamento do caso de usuário não encontrado (feedback para o remetente, sem crash)

**Concluída quando:** apenas o destinatário e o remetente veem a mensagem privada.

### Etapa 8 — Notificações de entrada/saída
**Objetivo:** eventos automáticos para a sala.

- Ao conectar: broadcast `"Fulano entrou"`
- Ao desconectar (detectado via `CancellationToken` cancelado ou stream fechado): broadcast `"Fulano saiu"`

**Concluída quando:** fechar o client de forma abrupta (Ctrl+C) ainda gera a notificação de saída no server.

---

## Conceitos de C# Praticados

Classes · Interfaces · Records · Collections · LINQ · Tasks · Async/Await · Injeção de Dependência · Eventos · `CancellationToken` · `ConcurrentDictionary` · Generics

## Conceitos de gRPC Praticados

Unary RPC · Server Streaming · Client Streaming · Bidirectional Streaming (foco principal) · Protocol Buffers · Geração automática de código · Canais (`GrpcChannel`) · Stubs do cliente · Serviços gRPC

---

## Resultado

O gRPConnect cobre um cenário clássico de uso do gRPC, demonstrando a criação de contratos com Protocol Buffers, comunicação bidirecional por streaming, gerenciamento de múltiplas conexões simultâneas e uma aplicação estruturada em camadas — competências valorizadas em projetos de microsserviços e aplicações distribuídas.
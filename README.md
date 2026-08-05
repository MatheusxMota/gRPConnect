# gRPConnect

O **gRPConnect** é uma aplicação de chat em tempo real, desenvolvida com foco em alta performance e comunicação bidirecional utilizando o ecossistema gRPC e .NET 10.0.

## Arquitetura do Projeto

O projeto segue uma estrutura modular para garantir a separação de responsabilidades:

- **`Chat.Client`**: Aplicação cliente responsável pela interface e interação com o usuário, consumindo o serviço gRPC.
- **`Chat.Contracts`**: Contém os arquivos `.proto` (Protocol Buffers) que definem o contrato de comunicação entre cliente e servidor.
- **`Chat.Server`**: Servidor gRPC que gerencia conexões, salas de chat e o roteamento de mensagens.
- **`Chat.Shared`**: Modelos e lógica compartilhados entre os componentes.

## Tecnologias Utilizadas

- **.NET 10.0**: Framework principal para o desenvolvimento da aplicação.
- **gRPC**: Framework de RPC de alto desempenho, utilizado para a comunicação eficiente entre cliente e servidor.
- **Protocol Buffers (Protobuf)**: Mecanismo de serialização de dados estruturados para os contratos de interface.

## Técnicas e Conceitos Aplicados

- **Bidirectional Streaming**: Utilização de streams gRPC para permitir que cliente e servidor enviem mensagens simultaneamente de forma contínua.
- **Gestão de Conexões**: Uso de `ConcurrentDictionary` no servidor para gerenciar as conexões dos clientes de forma *thread-safe*.
- **Gerenciamento de Assincronia**: Implementação robusta de `async/await` e manipulação de fluxos assíncronos (`IAsyncEnumerable`).
- **CancellationToken**: Implementação estratégica para o cancelamento gracioso de operações, garantindo que conexões sejam encerradas corretamente e evitando vazamentos de recursos.
- **Injeção de Dependência**: Utilizada no servidor para gerenciar os serviços e dependências da aplicação.

## Como Executar

### Requisitos
- .NET SDK 10.0 ou superior.

### Comandos
Na raiz do repositório, execute:

1. **Iniciar o servidor**:
   ```bash
   dotnet run --project src/Chat.Server
   ```

2. **Iniciar o cliente** (em outro terminal):
   ```bash
   dotnet run --project src/Chat.Client
   ```

## Licença
Este projeto está licenciado sob a licença MIT.

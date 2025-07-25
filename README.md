# Windows сервис для подключения к кластеру RabbitMQ и оповещения подсистемы «FoxyLink» об событиях 

### Минимальные требования:
* .NET Core SDK 2.0.3 и выше (инсрумент для работы с `.csproj`)
* система Windows
* настройте данные доступа к кластеру **RabbitMQ** и **HTTP сервисам** «1С: Предприятие 8»
(src\FoxyLink.GlobalConfiguration\appsettings.json)
* **Привилегированная командная строка**: запустите консоль как администратор.

### Описание файла настроек `appsettings.json`
```json
{
  "HostData": {
    "ServiceName": "FoxyLinkService",
    "ServiceDisplayName": "FoxyLink.RabbitMQ Service",
    "ServiceDescription": "FoxyLink.RabbitMQ (extract, transform, deliver messages to the «1C:Enterprise 8» consumers)"
  },
  "AccessData": {
    "AppEndpoints": [
      {
        "Name": "yt11",
        "Login": "login",
        "Password": "password",
        "Schema": "http",
        "ServerName": "1cweb.ktc.local:9292",
        "PathOnServer": "yt11/hs/AppEndpoint/v1"
      },
      {
        "Name": "dt11",
        "Login": "login",
        "Password": "password",
        "Schema": "http",
        "ServerName": "wall-e.ktc.local:8085",
        "PathOnServer": "dt/hs/AppEndpoint/v1"
      },
      {
        "Name": "corezoid",
        "Login": "",
        "Password": "",
        "Schema": "https",
        "ServerName": "new.corezoid.com",
        "PathOnServer": "api/1/json"  
      }
    ],
    "RabbitMQ": {
      "AmqpUri": "amqp://login:password@host:port/vhost",
      "Queues": [
        {
          "Name": "foxylink",
          "NodesCount": 1,
          "PrefetchCount": 1
        },
        {
          "Name": "1c.foxylink",
          "NodesCount": 2,
          "PrefetchCount": 2
        }
      ],
      "RetryInMilliseconds": [ "30000", "60000", "300000", "900000", "1800000", "3600000", "7200000", "14400000" ]
    }
  }
}
```
#### `AppEndpoints` описывает данные доступа к HTTP-сервису платформы «1С:Предприятие 8»   
`Name` — уникальный идентификатор базы данных;  
`Login` — имя пользователя на указанном сервере в базе данных «1С:Предприятие 8»;  
`Password` — пароль пользователя на указанном сервере в базе данных «1С:Предприятие 8»;  
`Schema` — обычно это `http` или `https`;  
`ServerName` — хост и порт сервера, с которым осуществляется соединение;  
`PathOnServer` — cтрока `http`/`https`-ресурса, к которому будет отправлен запрос.

#### `RabbitMQ` описывает данные доступа к кластеру RabbitMQ  
`AmqpUri` — данные доступа к кластеру RabbitMQ;   
`Queues` — описывает список создаваемых очередей:  
• `Name` — имя создаваемой очереди;  
• `NodesCount` — количество параллельных потребителей(обработчиков) очереди;  
• `PrefetchCount` — количество сообщений доставляемых потребителю(обработчику) при событии Ack;  
`RetryInMilliseconds` — количество повторных попыток (равна количеству элементов в массиве) и время через которое будет выполнена повторная попытка в миллисекундах; Если значение задано будут созданы очереди `Name.retry`.     

### Установка сервиса
Starting with .NET Core 2.0, you don't have to run `dotnet restore` because it's run implicitly by all commands, such as `dotnet build` and `dotnet run`, that require a restore to occur.

```cmd
> cd src\FoxyLink.Core
> dotnet run -c Release --register-service
...
Successfully registered and started service "FoxyLink.RabbitMQ Service" ("FoxyLink.RabbitMQ (extract, transform, deliver messages to the «1C:Enterprise 8» consumers)")
```

После успешной установки и запуска сервиса в кластере RabbitMQ должен появится новый потребитель сообщений.

### Удаление сервиса

```cmd
> dotnet run --unregister-service
...
Successfully unregistered service "FoxyLink.RabbitMQ Service" ("FoxyLink.RabbitMQ (extract, transform, deliver messages to the «1C:Enterprise 8» consumers)")
```
Обратите внимание, что служба может отображаться как `disabled` в течение некоторого времени, пока все инструменты, обращающиеся к API сервисам windows, не будут закрыты.
Просмотрите этот [Stackoverflow вопрос](http://stackoverflow.com/questions/20561990/how-to-solve-the-specified-service-has-been-marked-for-deletion-error).

### Минимальный набор полей сообщения для доставки в «1С:Предприятие 8»
Для успешной доставки сообщения из очередей у каждого сообщения должно быть заполнено:
* `content_type` — тип содержимого, например: `application/json`, `text/html` и другие; 
* `type` ([FoxyLink](https://github.com/FoxyLinkIO/FoxyLink)) — должен состоять из четырех частей, например `erp.bunny.send.sync`.  
  * часть `erp` — должна соответствовать одному из заданых имен (поле `Name`) в файле `appsettings.json` в разделе `AppEndpoints`. Если соответствие найдено сообщение будет отправлено на указанный адрес HTTP-сервиса, в противном случае сообщение будет сброшено в очередь `foxylink.invalid`;  
  * `bunny` — предназначено для подсистемы [FoxyLink](https://github.com/FoxyLinkIO/FoxyLink) и описывает имя настроек обмена (Catalog.FL_Exchanges).  
  * `send` — идентифицирует операцию связанную с сообщением (Catalog.FL_Operations);
  * `sync` или `async` — указывает для подсистемы [FoxyLink](https://github.com/FoxyLinkIO/FoxyLink) как следует выполнить операцию: синхронно или ассинхронно соответственно. 
  
  Нет обязательной необходимости устанавливать подсистему [FoxyLink](https://github.com/FoxyLinkIO/FoxyLink) в «1С:Предприятие 8», вы можете самостоятельно обрабатывать сообщения на стороне базы данных, но для корректной работы сервиса данные два поля (`content_type` и `type`) должны всегда быть заполнены.
  
* `type` ([Corezoid](https://new.corezoid.com)) — должен состоять из четырех частей, например `corezoid.public.452145.17858a4a7a1c42339a40e05f4f989e79fcd7fb63`.
  * часть `corezoid` — должна соответствовать одному из заданых имен (поле `Name`) в файле `appsettings.json` в разделе `AppEndpoints`. Если соответствие найдено сообщение будет отправлено на указанный адрес HTTPS-сервиса, в противном случае сообщение будет сброшено в очередь `foxylink.invalid`; 
  * `public` — предназначено для облачной ОС [Corezoid](https://new.corezoid.com), поле статичное и не изменяется; 
  * `452145` — идентифицирует номер процесса облачной ОС [Corezoid](https://new.corezoid.com);
  * `17858a4a7a1c42339a40e05f4f989e79fcd7fb63` — 40-символьный идентификатор процеса [Corezoid](https://new.corezoid.com), после успешного выполнения будет начат новый процесс (например, `452145`) в системе [Corezoid](https://new.corezoid.com) и данные тела сообщения будут помещены в этот процесс.



# Ошибки и пожелания

Проекты с открытым исходным кодом развиваются более плавно, когда обсуждения публичны.

Если вы обнаружили ошибку, сообщите об этом в [FoxyLink GitHub Issues](https://github.com/pbazeliuk/FoxyLink/issues?state=open). Приветствуются подробные отчеты с вызовами стека, реальным и ожидаемым поведением.

Если у вас есть какие-либо вопросы, проблемы, связанные с использованием подсистемы FoxyLink или если вы хотите обсудить новые функции, посетите чат [Slack](https://foxylinkio.herokuapp.com/).

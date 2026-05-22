## Gold Price Tracker Application - Project Architecture Document

### 1. High-Level Architecture Overview

The Gold Price Tracker is an ASP.NET Core 8 application designed to monitor real-time gold prices in INR, provide notifications on significant price changes, and expose a web UI and API endpoint for current price information. It follows a clean architecture approach, separating concerns into distinct layers: Presentation (MVC/API), Application Services (Business Logic), and Infrastructure (Data Access/External Integrations).

### 2. Folder Structure

```
GoldPriceTracker/
├── Controllers/           # Handles incoming HTTP requests and prepares data for views or API responses.
│   ├── HomeController.cs    # MVC controller for the web UI.
│   └── GoldPriceController.cs # API controller for the /gold-price endpoint.
├── Models/                # Plain Old CLR Objects (POCOs) representing data structures.
│   ├── CurrencyApiResponse.cs # Model for deserializing external API responses.
│   ├── GoldPriceInfo.cs     # Stores current and previous gold price data, change percentage, and timestamp.
│   ├── GoldPriceViewModel.cs# ViewModel for the HomeController to pass data to the view.
│   └── TwilioSettings.cs    # Configuration settings for Twilio SMS integration.
├── Services/              # Contains business logic, interfaces, and background tasks.
│   ├── GoldPriceBackgroundService.cs # Hosted service for periodic gold price fetching and processing.
│   ├── GoldPriceCache.cs    # In-memory cache for storing gold price data.
│   ├── IGoldPriceCache.cs   # Interface for GoldPriceCache.
│   ├── INotificationService.cs# Interface for NotificationService.
│   └── NotificationService.cs # Handles sending notifications (console log and Twilio SMS).
├── Pages/                 # Contains Razor Pages (though MVC is now primary, this folder might remain if Razor Pages are also used).
│   ├── Index.cshtml         # Frontend UI for displaying gold price (Razor Page version).
│   └── Index.cshtml.cs      # Code-behind for the Razor Page.
├── Views/                 # Contains Razor views for MVC controllers.
│   ├── Home/                # Views specific to the HomeController.
│   │   └── Index.cshtml     # Main UI for displaying gold price via MVC.
├── appsettings.json       # Application configuration settings.
├── appsettings.Development.json # Development-specific configuration settings.
├── Program.cs             # Application startup, service registration, and HTTP request pipeline configuration.
├── GoldPriceTracker.csproj# Project file defining dependencies and build settings.
└── ... (other standard ASP.NET Core files like wwwroot/ for static assets, etc.)
```

### 3. Description of Important Files

*   **`Program.cs`**: This is the entry point of the application. It configures the services for Dependency Injection, sets up the HTTP request pipeline, and defines routing. It registers MVC controllers, Razor Pages, Swagger, custom services (`GoldPriceCache`, `NotificationService`), and the background hosted service (`GoldPriceBackgroundService`).
*   **`Controllers/HomeController.cs`**: An MVC controller responsible for handling requests to the root URL. It retrieves current gold price data from `IGoldPriceCache` and passes it to the `Index.cshtml` view via a `GoldPriceViewModel`.
*   **`Controllers/GoldPriceController.cs`**: An API controller that exposes a `GET /gold-price` endpoint. It returns the current gold price (per 10g INR), last updated timestamp, and percentage change in a JSON format.
*   **`Models/GoldPriceInfo.cs`**: A model class to hold the core gold price data, including the current and previous prices per 10g INR, the calculated percentage change, and the timestamp of the last update.
*   **`Services/GoldPriceCache.cs` (and `IGoldPriceCache.cs`)**: Implements an in-memory, thread-safe cache for the `GoldPriceInfo` object. It provides methods to get and set the current gold price, ensuring that the last fetched valid price is always available.
*   **`Services/GoldPriceBackgroundService.cs`**: This is a `BackgroundService` that runs as a long-running task. It periodically fetches gold prices from an external API, applies retry logic, converts prices to 10g INR, checks for significant (±1%) changes, and triggers notifications via `INotificationService`.
*   **`Services/NotificationService.cs` (and `INotificationService.cs`)**: Provides an abstraction for sending notifications. It currently supports console logging and integrates with Twilio for SMS notifications. Twilio credentials and an `IsEnabled` flag are read from configuration.
*   **`appsettings.json` / `appsettings.Development.json`**: Configuration files used to store settings like logging levels, allowed hosts, the polling interval for the background service, and Twilio API credentials.
*   **`Views/Home/Index.cshtml`**: The main Razor view that renders the frontend UI. It displays the current gold price, last updated time, price change percentage, and a status indicator (UP/DOWN/UNCHANGED). It includes a meta tag for auto-refresh every 30 seconds.

### 4. Interaction between APIs, Services, and UI

*   **UI (`Views/Home/Index.cshtml`)**: The web UI directly interacts with the `HomeController`. The `HomeController` fetches the latest gold price information from the `GoldPriceCache` and constructs a `GoldPriceViewModel` to render the view.
*   **API (`Controllers/GoldPriceController.cs`)**: The `GET /gold-price` API endpoint directly accesses the `GoldPriceCache` to retrieve and return the latest gold price data to API consumers.
*   **Services**: The `GoldPriceBackgroundService` is the central hub for data acquisition and processing. It uses `HttpClient` to call the external gold price API, the `GoldPriceCache` to store and retrieve price data, and the `NotificationService` to send alerts.

### 5. Details of Notification System (Twilio Integration)

The application employs a flexible notification system through the `INotificationService` interface. The `NotificationService` implementation currently supports two notification methods:
1.  **Console Logging**: All notifications are logged to the console, providing an immediate trace of alerts.
2.  **Twilio SMS**: If enabled and configured in `appsettings.json`, the `NotificationService` uses the Twilio SDK to send SMS messages to a predefined phone number. It reads the `AccountSid`, `AuthToken`, `FromPhoneNumber`, `ToPhoneNumber`, and `IsEnabled` flag from the `TwilioSettings` section in the configuration.

### 6. Background Service Workflow for Fetching Gold Prices

The `GoldPriceBackgroundService` operates on a configurable interval (default 1 minute) and executes the following workflow:
1.  **Fetch Data**: Makes an HTTP GET request to the external gold price API.
2.  **Retry Logic**: Implements a retry mechanism with exponential backoff (max 3 retries) for transient network failures.
3.  **Parse Response**: Deserializes the API response into a `CurrencyApiResponse` object.
4.  **Validation**: Checks if the response contains valid gold price data (XAU in INR, greater than 0).
5.  **Conversion**: Converts the gold price from per ounce to per 10 grams (using `1 troy ounce = 31.1035 grams`), rounding to 2 decimal places.
6.  **Caching**: Updates the `IGoldPriceCache` with the new 10g price, also storing the previous 10g price for change calculation.
7.  **Change Detection**: Compares the current 10g price with the previous cached 10g price. If the absolute percentage change is 1% or more, it proceeds to trigger a notification.
8.  **Notification Trigger**: Calls the `INotificationService` to send an alert message, including old price, new price, percentage change, and direction (increase/decrease).
9.  **Error Handling**: Logs errors for HTTP request failures, JSON parsing issues, and general exceptions, ensuring the service remains resilient.

### 7. Flow Diagram (Step-by-Step Execution from App Start to Sending Alerts)

```mermaid
graph TD
    A[Application Start] --> B{Program.cs Configuration}
    B --> C[Register Services (DI Container)]
    C --> D[Start GoldPriceBackgroundService]

    D -- Every Configured Interval (e.g., 1 min) --> E[Fetch Gold Price from External API]
    E --> F{API Call Successful?}
    F -- No (Retry) --> G[Retry Logic (up to 3 times with exponential backoff)]
    G -- Max Retries Failed --> H[Log Error & Use Last Cached Value]
    F -- Yes --> I{Response Valid (JSON, Gold Price > 0)?}
    I -- No --> H
    I -- Yes --> J[Convert Price: Ounce to 10g INR]
    J --> K[Update GoldPriceCache (Current & Previous 10g Price)]
    K --> L{Price Change >= 1%?}
    L -- No --> M[Continue Polling]
    L -- Yes --> N[Log Gold Price Alert (Old, New, % Change)]
    N --> O[Send Notification via INotificationService]
    O --> P{Twilio Enabled & Configured?}
    P -- No --> Q[Log Console Notification]
    P -- Yes --> R[Send Twilio SMS]
    Q --> M
    R --> M

    C --> S[Configure MVC Routing & Static Files]
    S --> T[Map API Endpoints (e.g., /gold-price)]
    S --> U[Map MVC Routes (e.g., /Home/Index)]

    SubGraph User Interaction
        V[User Accesses http://localhost:5234/] --> W[HomeController.Index()]
        W --> X[Get Price from IGoldPriceCache]
        X --> Y[Render Views/Home/Index.cshtml]
        Y -- Auto-refresh (30s) --> W
        Z[API Client Accesses /gold-price] --> AA[GoldPriceController.GetGoldPrice()]
        AA --> X
        X --> BB[Return JSON Response]
    End
```

### 8. Data Flow between Components

1.  **External API -> `GoldPriceBackgroundService`**: Raw gold price data (XAU in INR per ounce) is fetched from the external API.
2.  **`GoldPriceBackgroundService` -> `GoldPriceCache`**: Converted gold price (per 10g INR) and related metadata are stored in the in-memory cache.
3.  **`GoldPriceCache` -> `GoldPriceBackgroundService`**: The background service retrieves the previous gold price from the cache for change calculation.
4.  **`GoldPriceBackgroundService` -> `NotificationService`**: Notification messages are sent to the `NotificationService` when a significant price change is detected.
5.  **`NotificationService` -> Console/Twilio API**: The notification message is logged to the console and/or sent as an SMS via Twilio.
6.  **`GoldPriceCache` -> `HomeController`**: The `HomeController` retrieves the latest gold price information from the cache to display on the web UI.
7.  **`GoldPriceCache` -> `GoldPriceController`**: The `GoldPriceController` retrieves the latest gold price information from the cache to return via the API endpoint.
8.  **`HomeController` -> `GoldPriceViewModel` -> `Views/Home/Index.cshtml`**: Formatted gold price data is passed to the MVC view for rendering.

### 9. Error Handling Strategy

*   **API Call Failures**: The `GoldPriceBackgroundService` implements a retry mechanism with exponential backoff for `HttpRequestException`s. If retries are exhausted, an error is logged, and the service continues to use the last successfully cached price.
*   **JSON Deserialization Errors**: `JsonException`s during API response parsing are caught and logged, preventing the application from crashing and allowing it to use the last cached value.
*   **Invalid API Responses**: Checks are in place to handle `null` or invalid gold price data from the API, logging warnings and preventing corrupted data from being cached.
*   **General Exceptions**: A general `catch (Exception ex)` block in the `ExecuteAsync` method of `GoldPriceBackgroundService` ensures that any unhandled exceptions during the polling cycle are logged, preventing the background service from stopping unexpectedly.
*   **Frontend Error State**: The UI gracefully handles cases where `GoldPriceInfo` is not yet available, displaying a "Loading..." message.

### 10. Scalability and Future Improvements

#### Scalability:
*   **Stateless Services**: Most services are stateless (except `GoldPriceCache`), facilitating horizontal scaling by simply running multiple instances of the application.
*   **In-memory Cache**: While suitable for small to medium scale, the in-memory `GoldPriceCache` could be replaced with a distributed cache (e.g., Redis) for improved scalability and data consistency across multiple instances.
*   **Asynchronous Operations**: Extensive use of `async`/`await` ensures non-blocking I/O operations, improving application responsiveness.
*   **Background Service**: The `BackgroundService` pattern allows for offloading periodic tasks from the main request pipeline.

#### Future Improvements:
*   **Database Integration**: Implement a database (e.g., SQL Server, MongoDB) to persist gold price history, enabling analytics, historical trend visualization, and more robust caching mechanisms.
*   **Multiple Notification Channels**: Expand the `INotificationService` to support other notification types (email, push notifications via SignalR, Slack, etc.).
*   **Configurable Alert Thresholds**: Allow users to configure their own price change thresholds for notifications.
*   **User Management & Personalization**: Implement user authentication and authorization, allowing users to subscribe to alerts, set personal thresholds, and view customized dashboards.
*   **Advanced Analytics**: Integrate with data visualization tools or libraries to provide interactive charts and historical price analysis.
*   **Real-time UI Updates**: Replace the meta refresh with WebSockets (e.g., SignalR) for real-time, push-based updates to the frontend, providing a more dynamic user experience.
*   **API Key Management**: Securely manage API keys for external services (e.g., Twilio, gold price API) using Azure Key Vault or similar secrets management solutions.
*   **Unit and Integration Tests**: Implement a comprehensive suite of unit and integration tests to ensure code quality, reliability, and maintainability. This is crucial for a production-ready application.
*   **Health Checks**: Implement ASP.NET Core Health Checks to monitor the health and availability of the background service, external API connectivity, and other critical components.

This architecture provides a solid foundation for a robust and scalable gold price tracking application.
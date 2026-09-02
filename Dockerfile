FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first, isolated from the rest of the source, so the (much slower) NuGet
# package download only re-runs when the project file itself changes.
COPY Kanban.csproj .
RUN dotnet restore Kanban.csproj

COPY . .
# No --no-restore here: ASP.NET Core's static web assets discovery (which is what
# produces the _framework/blazor.web.js manifest entry the Blazor circuit needs to
# even connect) runs as part of restore too, and at the restore step above wwwroot/
# the rest of the source didn't exist yet. Publishing with --no-restore reuses that
# incomplete intermediate state and silently ships a manifest missing blazor.web.js
# (confirmed by reproducing both ways directly against this image's SDK). Restoring
# again here is cheap since the packages are already cached from the step above.
RUN dotnet publish Kanban.csproj -c Release -o /app/publish

# One-shot migration runner: applies pending EF Core migrations against
# ConnectionStrings__DefaultConnection, then exits. The app has no auto-migrate on
# startup (matches the existing manual `dotnet ef database update` dev workflow), so
# compose's `migrate` service runs this before `app` starts.
FROM build AS migrate
RUN dotnet tool install --global dotnet-ef
ENV PATH="${PATH}:/root/.dotnet/tools"
ENTRYPOINT ["dotnet", "ef", "database", "update", "--project", "Kanban.csproj"]

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "Kanban.dll"]

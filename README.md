# DotNetMicroservicesObservabilitySample

Sample application looking into observability of .NET microservices, using popular tools and technologies like OpenTelemetry, Prometheus, Grafana and (potentially) others

This repository is a companion to the blog post [Observing .NET microservices with OpenTelemetry - logs, traces and metrics](https://blog.codingmilitia.com/2023/09/05/observing-dotnet-microservices-with-opentelemetry-logs-traces-metrics/)

## Running the sample

The sample was prepared to be easy to run after cloning the repository. There are two approaches to get things going:

- Using Docker Compose - in the repo root, run `docker compose up -d`
- Using Kubernetes - in the repo root, run `docker compose build`, so the containers are created, then run `kubectl apply -f ./k8s/`, so things start up

After things are running, you can access Grafana, where you can see the logs, traces and metrics.

To generate some load, to see things happening in Grafana, you can use the [K6](https://k6.io) script present in the repo. To run it, you need to have K6 installed, then, in the repo root, you could do `k6 run --vus 10 --duration 120s ./k6/script.js`, which would execute 10 virtual users, making HTTP requests to the API for 2 minutes.

To query the telemetry data, Grafana ca be found at `http://localhost:3000`. The default username and password are both `admin`.
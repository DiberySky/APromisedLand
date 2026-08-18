"""
Aspire Dashboard OpenTelemetry 集成
自动将 Trace/Metrics/Logs 推送到 Aspire Dashboard
"""
import logging
import os
from opentelemetry import metrics, trace
from opentelemetry._logs import set_logger_provider
from opentelemetry.exporter.otlp.proto.grpc._log_exporter import OTLPLogExporter
from opentelemetry.exporter.otlp.proto.grpc.metric_exporter import OTLPMetricExporter
from opentelemetry.exporter.otlp.proto.grpc.trace_exporter import OTLPSpanExporter
from opentelemetry.sdk._logs import LoggerProvider, LoggingHandler
from opentelemetry.sdk._logs.export import BatchLogRecordProcessor
from opentelemetry.sdk.metrics import MeterProvider
from opentelemetry.sdk.metrics.export import PeriodicExportingMetricReader
from opentelemetry.sdk.resources import Resource
from opentelemetry.sdk.trace import TracerProvider
from opentelemetry.sdk.trace.export import BatchSpanProcessor
from opentelemetry.instrumentation.fastapi import FastAPIInstrumentor


def configure_telemetry(app, service_name: str = "nebula-graph-api"):
    """配置 OpenTelemetry，数据自动流向 Aspire Dashboard"""

    # Aspire Dashboard 默认 OTLP gRPC 端点
    otlp_endpoint = os.getenv(
        "OTEL_EXPORTER_OTLP_ENDPOINT", 
        "http://localhost:4317"
    )

    # 服务资源标识（Aspire 自动注入 OTEL_SERVICE_NAME）
    resource = Resource.create({
        "service.name": os.getenv("OTEL_SERVICE_NAME", service_name),
        "service.namespace": os.getenv("OTEL_RESOURCE_ATTRIBUTES", "").split("=")[1] 
                         if "=" in os.getenv("OTEL_RESOURCE_ATTRIBUTES", "") else "nebula",
        "service.instance.id": os.getenv("OTEL_RESOURCE_ATTRIBUTES", "")
    })

    # ========== Traces ==========
    trace_provider = TracerProvider(resource=resource)
    trace_provider.add_span_processor(
        BatchSpanProcessor(OTLPSpanExporter(endpoint=otlp_endpoint))
    )
    trace.set_tracer_provider(trace_provider)

    # ========== Metrics ==========
    metric_reader = PeriodicExportingMetricReader(
        OTLPMetricExporter(endpoint=otlp_endpoint)
    )
    meter_provider = MeterProvider(resource=resource, metric_readers=[metric_reader])
    metrics.set_meter_provider(meter_provider)

    # ========== Logs ==========
    logger_provider = LoggerProvider(resource=resource)
    logger_provider.add_log_record_processor(
        BatchLogRecordProcessor(OTLPLogExporter(endpoint=otlp_endpoint))
    )
    set_logger_provider(logger_provider)

    # 将 Python logging 重定向到 OTLP
    handler = LoggingHandler(level=logging.NOTSET, logger_provider=logger_provider)
    logging.getLogger().addHandler(handler)

    # 自动 instrument FastAPI（记录所有 HTTP 请求的 trace）
    FastAPIInstrumentor.instrument_app(app)

    return trace.get_tracer(__name__)

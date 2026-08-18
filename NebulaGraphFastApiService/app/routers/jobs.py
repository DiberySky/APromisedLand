"""Job management endpoints."""
from fastapi import APIRouter

from ..schemas.job import CompactIn, FlushIn
from ..services import job_service
from ..utils.response import ok

router = APIRouter(prefix="/jobs", tags=["jobs"])


@router.post("/compact", summary="Submit a compact job")
def compact(body: CompactIn):
    return ok(job_service.compact(space=body.space, graph=body.graph))


@router.post("/flush", summary="Submit a flush job")
def flush(body: FlushIn):
    return ok(job_service.flush(space=body.space, graph=body.graph))


@router.post("/stats/{space}", summary="Submit a stats job for a space")
def submit_stats(space: str):
    return ok(job_service.submit_stats(space))


@router.get("", summary="List all jobs")
def list_jobs():
    return ok(job_service.list_jobs())


@router.get("/{job_id}", summary="Show details of a job")
def show_job(job_id: int):
    return ok(job_service.get_job(job_id))


@router.post("/{job_id}/stop", summary="Stop a running job")
def stop_job(job_id: int):
    return ok(job_service.stop_job(job_id))


@router.post("/recover", summary="Recover finished jobs")
def recover_jobs():
    return ok(job_service.recover_job())

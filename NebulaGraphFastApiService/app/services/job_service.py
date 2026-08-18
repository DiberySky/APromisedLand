"""Job management service (compact / flush / stats / stop / list)."""
from ..database import db


class JobService:

    def compact(self, space=None, graph=None) -> dict:
        return self._submit_job("COMPACT", space, graph)

    def flush(self, space=None, graph=None) -> dict:
        return self._submit_job("FLUSH", space, graph)

    def submit_stats(self, space: str) -> dict:
        return self._submit_job("STATS", space)

    def submit_job(self, kind: str, space=None) -> dict:
        kind = kind.strip().upper()
        return self._submit_job(kind, space)

    def _submit_job(self, kind: str, space, graph) -> dict:
        with db.session_scope(space) as s:
            rows = s.query(f"SUBMIT JOB {kind};")
        # SUBMIT JOB returns the new job id(s)
        job_ids = []
        for r in rows:
            if isinstance(r, dict):
                jid = r.get("Job Id") or r.get("job_id") or r.get("id")
                if jid is not None:
                    job_ids.append(jid)
        return {"job": kind.lower(), "space": space, "job_ids": job_ids}

    def list_jobs(self) -> list:
        with db.session_scope() as s:
            return s.query("SHOW JOBS;")

    def get_job(self, job_id: int) -> list:
        with db.session_scope() as s:
            return s.query(f"SHOW JOB {int(job_id)};")

    def stop_job(self, job_id: int) -> dict:
        with db.session_scope() as s:
            s.execute(f"STOP JOB {int(job_id)};")
        return {"job_id": int(job_id), "stopped": True}

    def recover_job(self) -> dict:
        with db.session_scope() as s:
            s.execute("RECOVER JOB;")
        return {"recovered": True}

    def show_stats(self, space: str) -> list:
        from ..database import validate_ident
        validate_ident(space, "space")
        with db.session_scope(space) as s:
            return s.query("SHOW STATS;")

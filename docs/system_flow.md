Client
=> API (POST /jobs)
=> DB (store as Pending)

Worker
=> fetch Pending jobs
=> mark Processing
=> execute job
=> mark Success / Failed

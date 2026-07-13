# Commerce Return Resolution sample

ASP.NET sample demonstrating Agent Kit in a controlled side-effect workflow. The model-visible tools can inspect synthetic order data, run ProtoScript policy calculations, and create a pending refund proposal. They cannot approve or issue a refund. Approval is exposed only as the normal application endpoint `POST /api/refund-proposals/{proposalId}/approve`.

Replace the JSON repositories with real order/workflow services to adapt this sample while preserving the human approval boundary.

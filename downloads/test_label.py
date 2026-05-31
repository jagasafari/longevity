"""Probe the exact same Azure OpenAI vision call the photo-api makes."""
import base64, json, sys
from azure.identity import DefaultAzureCredential, get_bearer_token_provider
from azure.storage.blob import BlobServiceClient
from openai import AzureOpenAI

ACCOUNT   = "longevityphotos"
CONTAINER = "photos"
ENDPOINT  = "https://longevity-ai.cognitiveservices.azure.com/"
MODEL     = "gpt-4o-mini"
API_VER   = "2024-06-01"

SYSTEM = (
    "You read English text printed in photos for a vocabulary learning app. "
    "Respond ONLY with raw JSON: "
    '{"word": "<lowercase word>", '
    '"source": "netflix_caption" | "ai_image_with_word" | "other", '
    '"confidence": 0.0..1.0}.'
)

def fetch(name):
    cred = DefaultAzureCredential()
    svc = BlobServiceClient(
        account_url=f"https://{ACCOUNT}.blob.core.windows.net",
        credential=cred)
    return svc.get_container_client(CONTAINER).download_blob(name).readall()

def label(name, data):
    cred = DefaultAzureCredential()
    token = get_bearer_token_provider(
        cred, "https://cognitiveservices.azure.com/.default")
    client = AzureOpenAI(
        azure_endpoint=ENDPOINT,
        azure_ad_token_provider=token,
        api_version=API_VER)
    b64 = base64.b64encode(data).decode()
    media = "image/png" if name.lower().endswith(".png") else "image/jpeg"
    resp = client.chat.completions.create(
        model=MODEL,
        messages=[
            {"role": "system", "content": SYSTEM},
            {"role": "user", "content": [
                {"type": "text", "text":
                 f"Read the English text on this image (file: {name})."},
                {"type": "image_url",
                 "image_url": {"url": f"data:{media};base64,{b64}",
                               "detail": "high"}}
            ]}
        ])
    return resp.choices[0].message.content

if __name__ == "__main__":
    name = sys.argv[1] if len(sys.argv) > 1 else "20260308_074833.jpg"
    print(f"Fetching {name} ...", flush=True)
    data = fetch(name)
    print(f"  size={len(data)} bytes")
    print("Calling Azure OpenAI ...", flush=True)
    try:
        out = label(name, data)
        print("RESPONSE:", out)
    except Exception as e:
        print("ERROR:", type(e).__name__, str(e)[:800])
        raise

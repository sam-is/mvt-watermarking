document.getElementById("button").addEventListener("click", setUrl);

const requests = chrome.storage.local.get(['requestsLog'],
    result => {
        const requests = result.requestsLog;
        for (let i = 0; i < requests.length; i++) {
            const elem = document.getElementById('resultTable');
            elem.innerHTML += `<div>${requests[i]}</div>`;
        }
    });

function setUrl() {
    const url = [];
    url.push(document.getElementById('url').value);
    chrome.storage.local.set({ 'url': url });
}
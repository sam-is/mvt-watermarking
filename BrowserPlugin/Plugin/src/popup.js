const requests = chrome.storage.local.get(['requestsLog'],
    result => {
        const requests = result.requestsLog;
        for (let i = 0; i < requests.length; i++) {
            const elem = document.getElementById('resultTable');
            elem.innerHTML += `<div>${requests[i]}</div>`;
        }
    });

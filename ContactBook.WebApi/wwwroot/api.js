export class ApiError extends Error {
    status;
    validationErrors;
    constructor(status, message, validationErrors) {
        super(message);
        this.name = "ApiError";
        this.status = status;
        this.validationErrors = validationErrors;
    }
}
export class ContactsApi {
    baseUrl;
    constructor(baseUrl = "/api/v1/contacts") {
        this.baseUrl = baseUrl;
    }
    async list(search) {
        const url = new URL(this.baseUrl, window.location.origin);
        if (search && search.trim().length > 0) {
            url.searchParams.set("search", search.trim());
        }
        const res = await fetch(url.toString(), { headers: { Accept: "application/json" } });
        await this.ensureOk(res);
        return await res.json();
    }
    async getById(id) {
        const res = await fetch(`${this.baseUrl}/${encodeURIComponent(id)}`);
        await this.ensureOk(res);
        return await res.json();
    }
    async getByShortId(shortId) {
        const res = await fetch(`${this.baseUrl}/by-short-id/${encodeURIComponent(shortId)}`);
        await this.ensureOk(res);
        return await res.json();
    }
    async create(req) {
        const res = await fetch(this.baseUrl, {
            method: "POST",
            headers: { "Content-Type": "application/json", Accept: "application/json" },
            body: JSON.stringify(req)
        });
        await this.ensureOk(res);
        return await res.json();
    }
    async update(req) {
        const res = await fetch(this.baseUrl, {
            method: "PUT",
            headers: { "Content-Type": "application/json", Accept: "application/json" },
            body: JSON.stringify(req)
        });
        await this.ensureOk(res);
        return await res.json();
    }
    async delete(id) {
        const res = await fetch(`${this.baseUrl}/${encodeURIComponent(id)}`, { method: "DELETE" });
        await this.ensureOk(res);
    }
    async ensureOk(res) {
        if (res.ok)
            return;
        if (res.status === 400) {
            const body = await res.json().catch(() => ({}));
            throw new ApiError(400, body.title ?? "Ошибка валидации", body.errors);
        }
        if (res.status === 404)
            throw new ApiError(404, "Контакт не найден");
        if (res.status === 409) {
            const body = await res.json().catch(() => ({}));
            throw new ApiError(409, body.message ?? "Конфликт данных");
        }
        throw new ApiError(res.status, `Ошибка запроса (${res.status})`);
    }
}

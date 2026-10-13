export type ContactType = "Email" | "Phone";

export interface Contact {
    id: string;
    shortId: string;
    name: string;
    contact: string;
    type: ContactType;
}

export interface CreateContactRequest {
    name: string;
    contact: string;
    type: ContactType;
}

export interface UpdateContactRequest {
    id: string;
    name: string;
    contact: string;
    type: ContactType;
}

export class ApiError extends Error {
    public readonly status: number;
    public readonly validationErrors?: Record<string, string[]>;

    constructor(status: number, message: string, validationErrors?: Record<string, string[]>) {
        super(message);
        this.name = "ApiError";
        this.status = status;
        this.validationErrors = validationErrors;
    }
}

export class ContactsApi {
    private readonly baseUrl: string;

    constructor(baseUrl: string = "/api/v1/contacts") {
        this.baseUrl = baseUrl;
    }

    async list(search?: string): Promise<Contact[]> {
        const url = new URL(this.baseUrl, window.location.origin);
        if (search && search.trim().length > 0) {
            url.searchParams.set("search", search.trim());
        }
        const res = await fetch(url.toString(), { headers: { Accept: "application/json" } });
        await this.ensureOk(res);
        return await res.json() as Contact[];
    }

    async getById(id: string): Promise<Contact> {
        const res = await fetch(`${this.baseUrl}/${encodeURIComponent(id)}`);
        await this.ensureOk(res);
        return await res.json() as Contact;
    }

    async getByShortId(shortId: string): Promise<Contact> {
        const res = await fetch(`${this.baseUrl}/by-short-id/${encodeURIComponent(shortId)}`);
        await this.ensureOk(res);
        return await res.json() as Contact;
    }

    async create(req: CreateContactRequest): Promise<Contact> {
        const res = await fetch(this.baseUrl, {
            method: "POST",
            headers: { "Content-Type": "application/json", Accept: "application/json" },
            body: JSON.stringify(req)
        });
        await this.ensureOk(res);
        return await res.json() as Contact;
    }

    async update(req: UpdateContactRequest): Promise<Contact> {
        const res = await fetch(this.baseUrl, {
            method: "PUT",
            headers: { "Content-Type": "application/json", Accept: "application/json" },
            body: JSON.stringify(req)
        });
        await this.ensureOk(res);
        return await res.json() as Contact;
    }

    async delete(id: string): Promise<void> {
        const res = await fetch(`${this.baseUrl}/${encodeURIComponent(id)}`, { method: "DELETE" });
        await this.ensureOk(res);
    }

    private async ensureOk(res: Response): Promise<void> {
        if (res.ok) return;

        if (res.status === 400) {
            const body = await res.json().catch(() => ({})) as {
                title?: string;
                errors?: Record<string, string[]>;
            };
            throw new ApiError(400, body.title ?? "Ошибка валидации", body.errors);
        }

        if (res.status === 404) throw new ApiError(404, "Контакт не найден");

        if (res.status === 409) {
            const body = await res.json().catch(() => ({})) as { message?: string };
            throw new ApiError(409, body.message ?? "Конфликт данных");
        }

        throw new ApiError(res.status, `Ошибка запроса (${res.status})`);
    }
}